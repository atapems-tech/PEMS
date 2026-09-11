namespace PEMS.Application.Exceptions;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۲۸: خطاهای معنادار سطح-Application
/// برای زیرساخت ذخیره‌سازی - پیام هرگز نباید اعتبارنامه/جزئیات داخلی زیرساخت را افشا کند
/// (بند ۱۵/۲۸: هرگز Stack Trace یا رمز عبور به کاربر نهایی نمایش داده نشود).
/// </summary>
public class StorageException : Exception
{
    public string Code { get; }

    public StorageException(string code, string message) : base(message)
    {
        Code = code;
    }

    public StorageException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}

public static class StorageErrorCodes
{
    public const string StorageProviderNotFound = "StorageProviderNotFound";
    public const string StorageProviderDisabled = "StorageProviderDisabled";
    public const string StorageUnavailable = "StorageUnavailable";
    public const string StorageAuthenticationFailed = "StorageAuthenticationFailed";
    public const string StoragePermissionDenied = "StoragePermissionDenied";
    public const string StoragePathInvalid = "StoragePathInvalid";
    public const string StorageWriteFailed = "StorageWriteFailed";
    public const string StorageReadFailed = "StorageReadFailed";
    public const string StorageDeleteFailed = "StorageDeleteFailed";
    public const string StorageConfigurationInvalid = "StorageConfigurationInvalid";
}
