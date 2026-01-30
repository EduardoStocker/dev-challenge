using System;
using System.Linq;
using System.Threading.Tasks;
using Desafio.Umbler.Exceptions;
using Desafio.Umbler.Models;
using Desafio.Umbler.Models.DTOs;
using Desafio.Umbler.Repositories.Interfaces;
using Desafio.Umbler.Service.Interfaces;
using Desafio.Umbler.Services.Interfaces;
using Desafio.Umbler.Validators;
using DnsClient;
using Whois.NET;

namespace Desafio.Umbler.Services
{
    public class DomainService : IDomainService
    {
        private readonly IDomainRepository _domainRepository;
        private readonly IWhoisClient _whoisClient;
        private readonly ILookupClient _lookupClient;

        public DomainService(
        IDomainRepository repository,
        IWhoisClient whoisClient,
        ILookupClient lookupClient)
        {
            _domainRepository = repository;
            _whoisClient = whoisClient;
            _lookupClient = lookupClient;
        }

        public DomainService(IDomainRepository domainRepository)
        {
            _domainRepository = domainRepository;
        }

        public async Task<DomainDto> GetDomainInfoAsync(string domainName)
        {
            var validationResult = DomainValidator.Validate(domainName);
            if (!validationResult.IsValid)
            {
                throw new DomainValidationException(validationResult.ErrorMessage);
            }

            var cleanDomain = validationResult.CleanedValue;

            try
            {
                var domain = await _domainRepository.GetByNameAsync(cleanDomain);

                if (domain == null)
                {
                    domain = await CreateNewDomainAsync(cleanDomain);
                    await _domainRepository.AddAsync(domain);
                }
                else if (IsDomainExpired(domain))
                {
                    domain = await UpdateDomainAsync(domain, cleanDomain);
                    await _domainRepository.UpdateAsync(domain);
                }

                await _domainRepository.SaveChangesAsync();

                return MapToDto(domain);
            }
            catch (DomainValidationException)
            {
                throw;
            }
            catch (Exception)
            {
                return new DomainDto
                {
                    Name = cleanDomain,
                    Ip = "N/A",
                    HostedAt = "N/A",
                    WhoIs = "Informação não disponível"
                };
            }

        }

        private bool IsDomainExpired(Domain domain)
        {
            if (domain.Ttl <= 0)
                return false;

            return DateTime.Now.Subtract(domain.UpdatedAt).TotalMinutes > domain.Ttl;
        }

        private async Task<Domain> CreateNewDomainAsync(string domainName)
        {
            try
            {
                var domainInfo = await FetchDomainInfoAsync(domainName);

                return new Domain
                {
                    Name = domainName,
                    Ip = domainInfo.Ip ?? "N/A",
                    UpdatedAt = DateTime.Now,
                    WhoIs = domainInfo.WhoIs ?? "Informação não disponível",
                    Ttl = domainInfo.Ttl,
                    HostedAt = domainInfo.HostedAt ?? "N/A"
                };
            }
            catch
            {
                return new Domain
                {
                    Name = domainName,
                    Ip = "N/A",
                    UpdatedAt = DateTime.Now,
                    WhoIs = "Informação não disponível",
                    Ttl = 0,
                    HostedAt = "N/A"
                };
            }
        }


        private async Task<Domain> UpdateDomainAsync(Domain domain, string domainName)
        {
            var domainInfo = await FetchDomainInfoAsync(domainName);

            domain.Name = domainName;
            domain.Ip = domainInfo.Ip;
            domain.UpdatedAt = DateTime.Now;
            domain.WhoIs = domainInfo.WhoIs;
            domain.Ttl = domainInfo.Ttl;
            domain.HostedAt = domainInfo.HostedAt;

            return domain;
        }

        private async Task<(string Ip, string WhoIs, int Ttl, string HostedAt)> FetchDomainInfoAsync(string domainName)
        {
            try
            {
                var whoisResponse = await QueryWhoisAsync(domainName);
                var dnsInfo = await QueryDnsAsync(domainName);
                var hostedAt = await GetHostedAtAsync(dnsInfo.Ip);

                return (dnsInfo.Ip, whoisResponse, dnsInfo.Ttl, hostedAt);
            }
            catch (Exception ex)
            {
                throw new ExternalServiceException(
                    $"Erro ao consultar serviços externos para o domínio '{domainName}'.", ex);
            }
        }

        private async Task<string> QueryWhoisAsync(string domainName)
        {
            try
            {
                var response = await WhoisClient.QueryAsync(domainName);
                return response?.Raw ?? "Informação não disponível";
            }
            catch (Exception ex)
            {
                return $"Erro ao consultar WhoIs: {ex.Message}";
            }
        }

        private async Task<(string Ip, int Ttl)> QueryDnsAsync(string domainName)
        {
            try
            {
                var lookup = new LookupClient();
                var result = await lookup.QueryAsync(domainName, QueryType.ANY);
                var record = result.Answers.ARecords().FirstOrDefault();

                if (record == null)
                {
                    return ("N/A", 0);
                }

                var ip = record.Address?.ToString() ?? "N/A";
                var ttl = record.TimeToLive;

                return (ip, ttl);
            }
            catch (Exception)
            {
                return ("N/A", 0);
            }
        }

        private async Task<string> GetHostedAtAsync(string ip)
        {
            if (string.IsNullOrEmpty(ip) || ip == "N/A")
                return "N/A";

            try
            {
                var hostResponse = await WhoisClient.QueryAsync(ip);

                if (!string.IsNullOrEmpty(hostResponse?.Raw))
                {
                    var lines = hostResponse.Raw.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("Organization:", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("OrgName:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(':');
                            if (parts.Length > 1)
                            {
                                return parts[1].Trim();
                            }
                        }
                    }
                }

                return hostResponse?.OrganizationName ?? "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        private DomainDto MapToDto(Domain domain)
        {
            if (domain == null)
                return null;

            return new DomainDto
            {
                Name = domain.Name ?? "N/A",
                Ip = domain.Ip ?? "N/A",
                HostedAt = domain.HostedAt ?? "N/A",
                WhoIs = domain.WhoIs ?? "Informação não disponível"
            };
        }
    }
}