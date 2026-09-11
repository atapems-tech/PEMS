using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management»: پیکربندی یک مقصد ذخیره‌سازی فیزیکی
/// (Local/NetworkShare/Ftp/Sftp)، قابل مدیریت توسط مدیر سیستم بدون نیاز به تغییر کد یا
/// appsettings.json. این جدول کاملاً افزودنی است - IFileStorageService/LocalFileStorageProvider
/// موجود (که ~۸ کنترلر واقعی از آن استفاده می‌کنند) دست‌نخورده باقی می‌مانند؛ این زیرساخت جدید
/// برای مسیرهای آپلود آینده/تازه‌پیکربندی‌شده است، نه جایگزین فوری آن‌ها.
///
/// EncryptedPassword هرگز متن‌خام نیست - با ICredentialProtector (بر پایه‌ی
/// Microsoft.AspNetCore.DataProtection) رمزنگاری می‌شود؛ در هیچ DTO/View/Log/AuditLog رمزگشایی‌شده
/// نمایش داده نمی‌شود.
/// </summary>
public class StorageProvider : BaseEntity
{
    [Key]
    public int StorageProviderId { get; set; }

    public string Name { get; set; } = string.Empty;

    // کد کوتاه یکتا برای ارجاع در Routing - مثلاً "ENG-SFTP"
    public string Code { get; set; } = string.Empty;

    // Local, NetworkShare, Ftp, Sftp - طبق StorageProviderTypes
    public string ProviderType { get; set; } = StorageProviderTypes.Local;

    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }

    // هرگز متن‌خام - همیشه از طریق ICredentialProtector رمزنگاری/رمزگشایی می‌شود
    public string? EncryptedPassword { get; set; }

    // برای Local: مسیر پوشه؛ برای NetworkShare: مسیر UNC؛ برای Ftp/Sftp: مسیر ریشه‌ی سمت سرور
    public string RootPath { get; set; } = string.Empty;

    // فقط برای ارائه‌های آینده که URL عمومی مستقیم دارند (مثلاً S3/Azure Blob) - فعلاً استفاده نمی‌شود
    public string? BaseUrl { get; set; }

    public bool UseSsl { get; set; }
    public bool UsePassiveMode { get; set; } = true;

    public bool AllowRead { get; set; } = true;
    public bool AllowWrite { get; set; } = true;
    public bool AllowDelete { get; set; } = true;

    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }

    public DateTime? LastHealthCheckAt { get; set; }
    public string LastHealthStatus { get; set; } = StorageHealthStatuses.Unknown;
    public string? LastHealthMessage { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<StorageRoute> Routes { get; set; } = new List<StorageRoute>();
}

public static class StorageProviderTypes
{
    public const string Local = "Local";
    public const string NetworkShare = "NetworkShare";
    public const string Ftp = "Ftp";
    public const string Sftp = "Sftp";

    public static readonly string[] All = { Local, NetworkShare, Ftp, Sftp };

    // طبق بند ۴ سند: امکان افزودن بعدی Minio/S3/AzureBlob بدون تغییر مدل Domain - فقط این لیست
    // و Factory مربوطه گسترش می‌یابند.
    public static bool RequiresNetworkCredentials(string providerType) =>
        providerType is Ftp or Sftp;
}

public static class StorageHealthStatuses
{
    public const string Online = "Online";
    public const string Offline = "Offline";
    public const string AuthenticationFailed = "AuthenticationFailed";
    public const string PermissionDenied = "PermissionDenied";
    public const string Disabled = "Disabled";
    public const string Unknown = "Unknown";
}
