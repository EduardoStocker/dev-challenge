using System;
using System.Threading.Tasks;
using Desafio.Umbler.Controllers;
using Desafio.Umbler.Exceptions;
using Desafio.Umbler.Models;
using Desafio.Umbler.Models.DTOs;
using Desafio.Umbler.Repositories;
using Desafio.Umbler.Repositories.Interfaces;
using Desafio.Umbler.Service.Interfaces;
using Desafio.Umbler.Services;
using Desafio.Umbler.Services.Interfaces;
using Desafio.Umbler.Validators;
using DnsClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Desafio.Umbler.Test
{
    [TestClass]
    public class HomeControllerTests
    {
        [TestMethod]
        public void Home_Index_Returns_View()
        {
            //arrange 
            var controller = new HomeController();

            //act
            var response = controller.Index();
            var result = response as ViewResult;

            //assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Home_Error_Returns_View_With_Model()
        {
            //arrange 
            var controller = new HomeController();
            controller.ControllerContext = new ControllerContext();
            controller.ControllerContext.HttpContext = new DefaultHttpContext();

            //act
            var response = controller.Error();
            var result = response as ViewResult;
            var model = result.Model as ErrorViewModel;

            //assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(model);
        }
    }

    [TestClass]
    public class DomainControllerTests
    {
        [TestMethod]
        public async Task Get_ValidDomain_Returns_OkResult_WithMockedService()
        {
            //arrange 
            var mockService = new Mock<IDomainService>();
            var domainDto = new DomainDto
            {
                Name = "umbler.com",
                Ip = "192.0.78.24",
                HostedAt = "Umbler",
                WhoIs = "Domain Name: UMBLER.COM..."
            };

            mockService
                .Setup(s => s.GetDomainInfoAsync("umbler.com"))
                .ReturnsAsync(domainDto);

            var controller = new DomainController(mockService.Object);

            //act
            var response = await controller.Get("umbler.com");
            var result = response as OkObjectResult;
            var obj = result.Value as DomainDto;

            //assert
            Assert.IsNotNull(result);
            Assert.AreEqual(200, result.StatusCode);
            Assert.IsNotNull(obj);
            Assert.AreEqual("umbler.com", obj.Name);

            mockService.Verify(s => s.GetDomainInfoAsync("umbler.com"), Times.Once);
        }

        [TestMethod]
        public async Task Get_EmptyDomain_Returns_BadRequest()
        {
            //arrange 
            var mockService = new Mock<IDomainService>();
            var controller = new DomainController(mockService.Object);

            //act
            var response = await controller.Get("");
            var result = response as BadRequestObjectResult;

            //assert
            Assert.IsNotNull(result);
            Assert.AreEqual(400, result.StatusCode);

            mockService.Verify(s => s.GetDomainInfoAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task Get_WhitespaceDomain_Returns_BadRequest()
        {
            //arrange 
            var mockService = new Mock<IDomainService>();
            var controller = new DomainController(mockService.Object);

            //act
            var response = await controller.Get("   ");
            var result = response as BadRequestObjectResult;

            //assert
            Assert.IsNotNull(result);
            Assert.AreEqual(400, result.StatusCode);
        }

        [TestMethod]
        public async Task Get_InvalidDomain_Returns_BadRequest()
        {
            //arrange 
            var mockService = new Mock<IDomainService>();
            mockService
                .Setup(s => s.GetDomainInfoAsync(It.IsAny<string>()))
                .ThrowsAsync(new DomainValidationException("Formato inválido"));

            var controller = new DomainController(mockService.Object);

            //act
            var response = await controller.Get("invalid domain");
            var result = response as BadRequestObjectResult;
            var errorResponse = result.Value as ErrorResponse;

            //assert
            Assert.IsNotNull(result);
            Assert.AreEqual(400, result.StatusCode);
            Assert.IsNotNull(errorResponse);
            Assert.AreEqual("Formato inválido", errorResponse.Message);
            Assert.AreEqual("DOMAIN_INVALID", errorResponse.ErrorCode);
        }

        [TestMethod]
        public async Task Get_ExternalServiceError_Returns_ServiceUnavailable()
        {
            //arrange 
            var mockService = new Mock<IDomainService>();
            mockService
                .Setup(s => s.GetDomainInfoAsync(It.IsAny<string>()))
                .ThrowsAsync(new ExternalServiceException("Serviço indisponível", new Exception()));

            var controller = new DomainController(mockService.Object);

            //act
            var response = await controller.Get("umbler.com");
            var result = response as ObjectResult;
            var errorResponse = result.Value as ErrorResponse;

            //assert
            Assert.IsNotNull(result);
            Assert.AreEqual(503, result.StatusCode);
            Assert.IsNotNull(errorResponse);
            Assert.AreEqual("SERVICE_UNAVAILABLE", errorResponse.ErrorCode);
        }

        [TestMethod]
        public async Task Get_UnexpectedException_Returns_InternalServerError()
        {
            //arrange 
            var mockService = new Mock<IDomainService>();
            mockService
                .Setup(s => s.GetDomainInfoAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Erro inesperado"));

            var controller = new DomainController(mockService.Object);

            //act
            var response = await controller.Get("umbler.com");
            var result = response as ObjectResult;
            var errorResponse = result.Value as ErrorResponse;

            //assert
            Assert.IsNotNull(result);
            Assert.AreEqual(500, result.StatusCode);
            Assert.IsNotNull(errorResponse);
            Assert.AreEqual("INTERNAL_ERROR", errorResponse.ErrorCode);
        }
    }

    [TestClass]
    public class DomainServiceTests
    {
        [TestMethod]
        public async Task GetDomainInfo_ExistingDomain_InCache_Returns_Domain()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "ServiceTest_CachedDomain_" + Guid.NewGuid())
                .Options;

            var domain = new Domain
            {
                Name = "cached.com",
                Ip = "1.2.3.4",
                UpdatedAt = DateTime.Now,
                HostedAt = "Test Host",
                Ttl = 3600, // 1 hora - não expirado
                WhoIs = "Cached WHOIS data"
            };

            using (var db = new DatabaseContext(options))
            {
                db.Domains.Add(domain);
                await db.SaveChangesAsync();
            }

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);
                var service = new DomainService(repository);

                //act
                var result = await service.GetDomainInfoAsync("cached.com");

                //assert
                Assert.IsNotNull(result);
                Assert.AreEqual("cached.com", result.Name);
                Assert.AreEqual("1.2.3.4", result.Ip);
                Assert.AreEqual("Cached WHOIS data", result.WhoIs);
            }
        }

        [TestMethod]
        public async Task GetDomainInfo_ExpiredDomain_Updates_From_ExternalServices()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "ServiceTest_ExpiredDomain_" + Guid.NewGuid())
                .Options;

            var domain = new Domain
            {
                Name = "umbler.com",
                Ip = "0.0.0.0",
                UpdatedAt = DateTime.Now.AddHours(-2),
                HostedAt = "Old Host",
                Ttl = 1, // 1 segundo - já expirado
                WhoIs = "Old WHOIS"
            };

            using (var db = new DatabaseContext(options))
            {
                db.Domains.Add(domain);
                await db.SaveChangesAsync();
            }

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);
                var service = new DomainService(repository);

                //act
                var result = await service.GetDomainInfoAsync("umbler.com");

                //assert
                Assert.IsNotNull(result);
                Assert.AreEqual("umbler.com", result.Name);
                Assert.AreNotEqual("0.0.0.0", result.Ip); // IP foi atualizado
                Assert.AreNotEqual("Old WHOIS", result.WhoIs); // WHOIS foi atualizado
            }
        }

        [TestMethod]
        public async Task GetDomainInfo_NewDomain_FetchesFrom_ExternalServices()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "ServiceTest_NewDomain_" + Guid.NewGuid())
                .Options;

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);
                var service = new DomainService(repository);

                //act
                var result = await service.GetDomainInfoAsync("umbler.com");

                //assert
                Assert.IsNotNull(result);
                Assert.AreEqual("umbler.com", result.Name);
                Assert.IsNotNull(result.Ip);
                Assert.AreNotEqual("N/A", result.Ip);
                Assert.IsNotNull(result.WhoIs);
                Assert.IsTrue(result.WhoIs.Length > 0);
            }
        }

        [TestMethod]
        public async Task GetDomainInfo_InvalidDomain_Throws_ValidationException()
        {
            //arrange 
            var mockRepository = new Mock<IDomainRepository>();
            var service = new DomainService(mockRepository.Object);

            //act & assert
            await Assert.ThrowsExceptionAsync<DomainValidationException>(
                async () => await service.GetDomainInfoAsync("invalid domain!")
            );

            mockRepository.Verify(r => r.GetByNameAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task GetDomainInfo_CleansDomain_BeforeProcessing()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "ServiceTest_CleanDomain_" + Guid.NewGuid())
                .Options;

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);
                var service = new DomainService(repository);

                //act
                var result = await service.GetDomainInfoAsync("https://www.umbler.com/path?query=1");

                //assert
                Assert.IsNotNull(result);
                Assert.AreEqual("umbler.com", result.Name);
            }
        }

        [TestMethod]
        public async Task GetDomainInfo_WithMockedRepository_Returns_CachedDomain()
        {
            //arrange 
            var mockRepository = new Mock<IDomainRepository>();
            var domain = new Domain
            {
                Id = 1,
                Name = "test.com",
                Ip = "1.1.1.1",
                UpdatedAt = DateTime.Now,
                HostedAt = "Test Host",
                Ttl = 3600,
                WhoIs = "Test WHOIS"
            };

            mockRepository
                .Setup(r => r.GetByNameAsync("test.com"))
                .ReturnsAsync(domain);

            mockRepository
                .Setup(r => r.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            var service = new DomainService(mockRepository.Object);

            //act
            var result = await service.GetDomainInfoAsync("test.com");

            //assert
            Assert.IsNotNull(result);
            Assert.AreEqual("test.com", result.Name);
            Assert.AreEqual("1.1.1.1", result.Ip);

            mockRepository.Verify(r => r.GetByNameAsync("test.com"), Times.Once);
            mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
            mockRepository.Verify(r => r.AddAsync(It.IsAny<Domain>()), Times.Never);
            mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Domain>()), Times.Never);
        }
    }

    [TestClass]
    public class RepositoryTests
    {
        [TestMethod]
        public async Task GetByNameAsync_ExistingDomain_Returns_Domain()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "RepoTest_GetByName_" + Guid.NewGuid())
                .Options;

            var domain = new Domain
            {
                Id = 1,
                Name = "test.com",
                Ip = "192.168.0.1",
                UpdatedAt = DateTime.Now
            };

            using (var db = new DatabaseContext(options))
            {
                db.Domains.Add(domain);
                await db.SaveChangesAsync();
            }

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);

                //act
                var result = await repository.GetByNameAsync("test.com");

                //assert
                Assert.IsNotNull(result);
                Assert.AreEqual(domain.Name, result.Name);
                Assert.AreEqual(domain.Ip, result.Ip);
            }
        }

        [TestMethod]
        public async Task GetByNameAsync_NonExistentDomain_Returns_Null()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "RepoTest_NotFound_" + Guid.NewGuid())
                .Options;

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);

                //act
                var result = await repository.GetByNameAsync("nonexistent.com");

                //assert
                Assert.IsNull(result);
            }
        }

        [TestMethod]
        public async Task AddAsync_NewDomain_Saves_Successfully()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "RepoTest_Add_" + Guid.NewGuid())
                .Options;

            var domain = new Domain
            {
                Name = "newdomain.com",
                Ip = "192.168.0.1",
                UpdatedAt = DateTime.Now,
                Ttl = 300,
                HostedAt = "Test",
                WhoIs = "Test WHOIS"
            };

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);

                //act
                await repository.AddAsync(domain);
                await repository.SaveChangesAsync();
            }

            using (var db = new DatabaseContext(options))
            {
                //assert
                var saved = await db.Domains.FirstOrDefaultAsync(d => d.Name == "newdomain.com");
                Assert.IsNotNull(saved);
                Assert.AreEqual(domain.Name, saved.Name);
                Assert.AreEqual(domain.Ip, saved.Ip);
            }
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingDomain_Updates_Successfully()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "RepoTest_Update_" + Guid.NewGuid())
                .Options;

            var domain = new Domain
            {
                Name = "update.com",
                Ip = "1.1.1.1",
                UpdatedAt = DateTime.Now,
                Ttl = 300,
                HostedAt = "Old Host",
                WhoIs = "Old WHOIS"
            };

            using (var db = new DatabaseContext(options))
            {
                db.Domains.Add(domain);
                await db.SaveChangesAsync();
            }

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);
                var domainToUpdate = await db.Domains.FirstAsync(d => d.Name == "update.com");

                domainToUpdate.Ip = "2.2.2.2";
                domainToUpdate.HostedAt = "New Host";

                //act
                await repository.UpdateAsync(domainToUpdate);
                await repository.SaveChangesAsync();
            }

            using (var db = new DatabaseContext(options))
            {
                //assert
                var updated = await db.Domains.FirstAsync(d => d.Name == "update.com");
                Assert.AreEqual("2.2.2.2", updated.Ip);
                Assert.AreEqual("New Host", updated.HostedAt);
            }
        }
    }

    [TestClass]
    public class ValidatorTests
    {
        [TestMethod]
        public void Validate_ValidDomain_Returns_Success()
        {
            //act
            var result = DomainValidator.Validate("umbler.com");

            //assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("umbler.com", result.CleanedValue);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void Validate_DomainWithProtocol_Cleans_And_Validates()
        {
            //act
            var result = DomainValidator.Validate("https://www.umbler.com");

            //assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("umbler.com", result.CleanedValue);
        }

        [TestMethod]
        public void Validate_DomainWithPath_Removes_Path()
        {
            //act
            var result = DomainValidator.Validate("umbler.com/path/to/page");

            //assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("umbler.com", result.CleanedValue);
        }

        [TestMethod]
        public void Validate_DomainWithQueryString_Removes_Query()
        {
            //act
            var result = DomainValidator.Validate("umbler.com?query=test&param=value");

            //assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("umbler.com", result.CleanedValue);
        }

        [TestMethod]
        public void Validate_ComplexUrl_Cleans_Properly()
        {
            //act
            var result = DomainValidator.Validate("https://www.example.com:8080/path?query=1#anchor");

            //assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("example.com:8080", result.CleanedValue);
        }

        [TestMethod]
        public void Validate_EmptyDomain_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate("");

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("O domínio não pode ser vazio.", result.ErrorMessage);
        }

        [TestMethod]
        public void Validate_NullDomain_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate(null);

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("O domínio não pode ser vazio.", result.ErrorMessage);
        }

        [TestMethod]
        public void Validate_WhitespaceDomain_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate("   ");

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("O domínio não pode ser vazio.", result.ErrorMessage);
        }

        [TestMethod]
        public void Validate_DomainWithSpaces_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate("invalid domain.com");

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("espaços"));
        }

        [TestMethod]
        public void Validate_DomainTooShort_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate("a.c");

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("mínimo"));
        }

        [TestMethod]
        public void Validate_DomainTooLong_Returns_Failure()
        {
            //act
            var longDomain = new string('a', 250) + ".com";
            var result = DomainValidator.Validate(longDomain);

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("máximo"));
        }

        [TestMethod]
        public void Validate_DomainWithoutExtension_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate("domain");

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("extensão"));
        }

        [TestMethod]
        public void Validate_DomainStartingWithDot_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate(".domain.com");

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("começar"));
        }

        [TestMethod]
        public void Validate_DomainEndingWithDot_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate("domain.com.");

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("terminar"));
        }

        [TestMethod]
        public void Validate_DomainWithConsecutiveDots_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate("domain..com");

            //assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("consecutivos"));
        }

        [TestMethod]
        public void Validate_DomainWithInvalidChars_Returns_Failure()
        {
            //act
            var result = DomainValidator.Validate("domain@test.com");

            //assert
            Assert.IsFalse(result.IsValid);
        }

        [TestMethod]
        public void CleanDomain_RemovesProtocolAndWww()
        {
            //act
            var result = DomainValidator.CleanDomain("https://www.umbler.com");

            //assert
            Assert.AreEqual("umbler.com", result);
        }

        [TestMethod]
        public void CleanDomain_RemovesQueryString()
        {
            //act
            var result = DomainValidator.CleanDomain("umbler.com?query=123");

            //assert
            Assert.AreEqual("umbler.com", result);
        }

        [TestMethod]
        public void CleanDomain_HandlesNull()
        {
            //act
            var result = DomainValidator.CleanDomain(null);

            //assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void CleanDomain_HandlesEmpty()
        {
            //act
            var result = DomainValidator.CleanDomain("");

            //assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void CleanDomain_ConvertsToLowerCase()
        {
            //act
            var result = DomainValidator.CleanDomain("UMBLER.COM");

            //assert
            Assert.AreEqual("umbler.com", result);
        }
    }

    [TestClass]
    public class IntegrationTests
    {
        [TestMethod]
        public async Task Integration_FullFlow_NewDomain()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "Integration_FullFlow_" + Guid.NewGuid())
                .Options;

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);
                var service = new DomainService(repository);
                var controller = new DomainController(service);

                //act
                var response = await controller.Get("umbler.com");
                var result = response as OkObjectResult;
                var domain = result.Value as DomainDto;

                //assert
                Assert.IsNotNull(result);
                Assert.AreEqual(200, result.StatusCode);
                Assert.IsNotNull(domain);
                Assert.AreEqual("umbler.com", domain.Name);
                Assert.IsNotNull(domain.Ip);
                Assert.AreNotEqual("N/A", domain.Ip);
                Assert.IsTrue(domain.WhoIs.Length > 0);
            }
        }

        [TestMethod]
        public async Task Integration_FullFlow_CachedDomain()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "Integration_Cached_" + Guid.NewGuid())
                .Options;

            var cachedDomain = new Domain
            {
                Name = "umbler.com",
                Ip = "192.0.78.24",
                UpdatedAt = DateTime.Now,
                HostedAt = "Umbler",
                Ttl = 3600,
                WhoIs = "Cached WHOIS"
            };

            using (var db = new DatabaseContext(options))
            {
                db.Domains.Add(cachedDomain);
                await db.SaveChangesAsync();
            }

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);
                var service = new DomainService(repository);
                var controller = new DomainController(service);

                //act
                var response = await controller.Get("umbler.com");
                var result = response as OkObjectResult;
                var domain = result.Value as DomainDto;

                //assert
                Assert.IsNotNull(result);
                Assert.AreEqual(200, result.StatusCode);
                Assert.IsNotNull(domain);
                Assert.AreEqual("umbler.com", domain.Name);
                Assert.AreEqual("192.0.78.24", domain.Ip);
                Assert.AreEqual("Cached WHOIS", domain.WhoIs);
            }
        }

        [TestMethod]
        public async Task Integration_Domain_With_Protocol_And_Path()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "Integration_WithProtocol_" + Guid.NewGuid())
                .Options;

            using (var db = new DatabaseContext(options))
            {
                var repository = new DomainRepository(db);
                var service = new DomainService(repository);
                var controller = new DomainController(service);

                //act
                var response = await controller.Get("https://www.umbler.com/br/hospedagem-de-sites");
                var result = response as OkObjectResult;
                var domain = result.Value as DomainDto;

                //assert
                Assert.IsNotNull(result);
                Assert.AreEqual(200, result.StatusCode);
                Assert.IsNotNull(domain);
                Assert.AreEqual("umbler.com", domain.Name); // Deve limpar protocolo, www e path
            }
        }
    }

    [TestClass]
    public class DomainInDatabaseTests
    {
        [TestMethod]
        public void Domain_In_Database()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "Find_searches_url")
                .Options;

            var domain = new Domain { Id = 1, Ip = "192.168.0.1", Name = "test.com", UpdatedAt = DateTime.Now, HostedAt = "umbler.corp", Ttl = 60, WhoIs = "Ns.umbler.com" };

            // Insert seed data into the database using one instance of the context
            using (var db = new DatabaseContext(options))
            {
                db.Domains.Add(domain);
                db.SaveChanges();
            }

            // Use a clean instance of the context to run the test
            using (var db = new DatabaseContext(options))
            {
                var controller = new DomainController(db);

                //act
                var response = controller.Get("test.com");
                var result = response.Result as OkObjectResult;
                var obj = result.Value as DomainDto;
                Assert.AreEqual(obj.Ip, domain.Ip);
                Assert.AreEqual(obj.Name, domain.Name);
            }
        }

        [TestMethod]
        public void Domain_Not_In_Database()
        {
            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "Find_searches_url")
                .Options;

            // Use a clean instance of the context to run the test
            using (var db = new DatabaseContext(options))
            {
                var controller = new DomainController(db);

                //act
                var response = controller.Get("test.com");
                var result = response.Result as OkObjectResult;
                var obj = result.Value as DomainDto;
                Assert.IsNotNull(obj);
            }
        }

        [TestMethod]
        public void Domain_Moking_LookupClient()
        {
            //arrange 
            var lookupClient = new Mock<ILookupClient>();
            var domainName = "test.com";

            var dnsResponse = new Mock<IDnsQueryResponse>();
            lookupClient.Setup(l => l.QueryAsync(domainName, QueryType.ANY, QueryClass.IN, System.Threading.CancellationToken.None)).ReturnsAsync(dnsResponse.Object);

            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "Find_searches_url")
                .Options;

            // Use a clean instance of the context to run the test
            using (var db = new DatabaseContext(options))
            {
                //inject lookupClient in controller constructor
                var repository = new DomainRepository(db);
                var service = new DomainService(repository);
                var controller = new DomainController(service);

                //act
                var response = controller.Get("test.com");
                var result = response.Result as OkObjectResult;
                var obj = result.Value as DomainDto;
                Assert.IsNotNull(obj);
            }
        }


        [TestMethod]
        public void Domain_Moking_WhoisClient()
        {
            //arrange
            //whois is a static class, we need to create a class to "wrapper" in a mockable version of WhoisClient
            var whoisClient = new Mock<IWhoisClient>();
            var domainName = "test.com";

            whoisClient.Setup(l => l.QueryAsync(domainName)).Return();

            //arrange 
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(databaseName: "Find_searches_url")
                .Options;

            // Use a clean instance of the context to run the test
            using (var db = new DatabaseContext(options))
            {
                //inject IWhoisClient in controller's constructor
                var controller = new DomainController(db/*,IWhoisClient, ILookupClient*/);

                //act
                var response = controller.Get("test.com");
                var result = response.Result as OkObjectResult;
                var obj = result.Value as DomainDto;
                Assert.IsNotNull(obj);
            }
        }
    }
}