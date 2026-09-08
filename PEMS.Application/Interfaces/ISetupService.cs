using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Quick Setup &amp; Domain Import Architecture Refactoring»: Quick Setup صرفاً
/// وضعیت واقعی داده‌ی موجود را می‌خواند و به Workflow های دامنه‌ای موجود لینک می‌دهد - هیچ
/// منطق کسب‌وکاری یا Mutation ای این‌جا تکرار نمی‌شود.
/// </summary>
public interface ISetupService
{
    Task<SetupStatusDto> GetStatusAsync();
}
