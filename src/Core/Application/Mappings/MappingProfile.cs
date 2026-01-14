
using AutoMapper;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Shared.Contracts.DTOs;

namespace Enterprise.Documentation.Core.Application.Mappings;

/// <summary>
/// AutoMapper profile for mapping between domain entities and DTOs.
/// Provides essential mapping configuration for domain objects.
/// Additional mappings will be added as corresponding DTOs are created.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        ConfigureDocumentMappings();
        ConfigureValueObjectMappings();
    }

    private void ConfigureDocumentMappings()
    {
        // Basic Document to DocumentDto mapping
        CreateMap<Document, DocumentDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.Value))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.ApprovalStatus, opt => opt.MapFrom(src => src.ApprovalStatus.Status))
            .ForMember(dest => dest.SecurityLevel, opt => opt.MapFrom(src => src.SecurityClassification.Level))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy.Value))
            .ForMember(dest => dest.ModifiedBy, opt => opt.MapFrom(src => src.ModifiedBy.Value));

        // Additional document mappings will be added when request/response DTOs are created
    }

    private void ConfigureValueObjectMappings()
    {
        // DocumentId mappings
        CreateMap<DocumentId, Guid>().ConvertUsing(src => src.Value);
        CreateMap<Guid, DocumentId>().ConvertUsing(src => new DocumentId(src));

        // UserId mappings
        CreateMap<UserId, Guid>().ConvertUsing(src => src.Value);
        CreateMap<Guid, UserId>().ConvertUsing(src => new UserId(src));

        // TemplateId mappings
        CreateMap<TemplateId, Guid>().ConvertUsing(src => src.Value);
        CreateMap<Guid, TemplateId>().ConvertUsing(src => new TemplateId(src));

        // AgentId mappings
        CreateMap<AgentId, Guid>().ConvertUsing(src => src.Value);
        CreateMap<Guid, AgentId>().ConvertUsing(src => new AgentId(src));

        // Basic value object mappings - additional mappings will be added as needed
    }
}