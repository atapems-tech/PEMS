namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۲: این DTO هرگز رمز عبور را حمل
/// نمی‌کند - فقط HasPassword (بولین) تا UI بتواند "********" نشان دهد. رمز عبور واقعی هرگز به
/// مرورگر نمی‌رسد.
/// </summary>
public class StorageProviderDto
{
    public int StorageProviderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public bool HasPassword { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public bool UseSsl { get; set; }
    public bool UsePassiveMode { get; set; }
    public bool AllowRead { get; set; }
    public bool AllowWrite { get; set; }
    public bool AllowDelete { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }
    public string LastHealthStatus { get; set; } = string.Empty;
    public string? LastHealthMessage { get; set; }
    public int ActiveRouteCount { get; set; }
}

/// <summary>
/// درخواست ساخت/ویرایش - Password فقط وقتی مقدار دارد تغییر می‌کند (Change Password)؛ خالی
/// گذاشتن در ویرایش یعنی رمز فعلی دست‌نخورده بماند.
/// </summary>
public class StorageProviderUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public bool UseSsl { get; set; }
    public bool UsePassiveMode { get; set; } = true;
    public bool AllowRead { get; set; } = true;
    public bool AllowWrite { get; set; } = true;
    public bool AllowDelete { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// طبق بند ۱۵ سند: نتیجه‌ی گام‌به‌گام Test Connection - هرگز Stack Trace یا اعتبارنامه به
/// کاربر نمایش داده نمی‌شود، فقط نتیجه‌ی هر گام.
/// </summary>
public class StorageHealthResultDto
{
    public string OverallStatus { get; set; } = string.Empty; // Online/Offline/AuthenticationFailed/PermissionDenied/Unknown
    public string? Message { get; set; }
    public List<StorageHealthStepResultDto> Steps { get; set; } = new();
}

public class StorageHealthStepResultDto
{
    public string Step { get; set; } = string.Empty; // Connection, Authentication, RootPath, Read, Write, Delete
    public bool Pass { get; set; }
    public string? Detail { get; set; }
}

public class StorageRouteDto
{
    public int StorageRouteId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public int StorageProviderId { get; set; }
    public string? StorageProviderName { get; set; }
    public string? RelativeRootPath { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
}

public class StorageRouteUpsertRequest
{
    public string Module { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public int StorageProviderId { get; set; }
    public string? RelativeRootPath { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// خروجی یک عملیات ذخیره‌ی واقعی از طریق IEnterpriseFileStorageService.
/// </summary>
public class EnterpriseStorageResultDto
{
    public int StorageProviderId { get; set; }
    public string StorageProviderCode { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime SavedAt { get; set; }
}
