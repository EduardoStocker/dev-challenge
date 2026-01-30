using System;
using System.Threading.Tasks;
using Desafio.Umbler.Exceptions;
using Desafio.Umbler.Models;
using Desafio.Umbler.Models.DTOs;
using Desafio.Umbler.Repositories;
using Desafio.Umbler.Service;
using Desafio.Umbler.Service.Interfaces;
using Desafio.Umbler.Services;
using Desafio.Umbler.Services.Interfaces;
using DnsClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

[ApiController]
[Route("api/[controller]")]
public class DomainController : ControllerBase
{
    private readonly IDomainService _domainService;

    [ActivatorUtilitiesConstructor]
    public DomainController(IDomainService domainService)
    {
        _domainService = domainService;
    }
    public DomainController(DatabaseContext db)
    {
        var repository = new DomainRepository(db);
        IWhoisClient whoisClient = new WhoisClientWrapper();
        ILookupClient lookupClient = new LookupClient();
        _domainService = new DomainService(
            repository,
            whoisClient,
            lookupClient
        );
    }

    public DomainController(DatabaseContext db, IWhoisClient whoisClient, ILookupClient lookupClient)
    {
        var repository = new DomainRepository(db);
        _domainService = new DomainService(
            repository,
            whoisClient,
            lookupClient
        );
    }

    [HttpGet("{domainName}")]
    public async Task<IActionResult> Get(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "O domínio não pode ser vazio.",
                ErrorCode = "DOMAIN_EMPTY"
            });
        }

        try
        {
            var result = await _domainService.GetDomainInfoAsync(domainName);
            return Ok(result);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Message = ex.Message,
                ErrorCode = "DOMAIN_INVALID"
            });
        }
        catch (ExternalServiceException ex)
        {
            return StatusCode(503, new ErrorResponse
            {
                Message = "Serviço temporariamente indisponível. Tente novamente mais tarde.",
                ErrorCode = "SERVICE_UNAVAILABLE",
                Details = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse
            {
                Message = "Erro interno ao processar a requisição.",
                ErrorCode = "INTERNAL_ERROR",
                Details = ex.Message
            });
        }
    }
}