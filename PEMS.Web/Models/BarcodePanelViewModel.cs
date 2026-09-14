using PEMS.Application.DTOs;

namespace PEMS.Web.Models;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System» بند «UI»: یک پارتیال مشترک برای تب Barcode روی هر
/// صفحه‌ی Details موجود (Equipment/MaterialItem/...) - نه یک کامپوننت جدا به‌ازای هر ماژول.
/// </summary>
public class BarcodePanelViewModel
{
    public string TargetType { get; set; } = "";
    public int TargetId { get; set; }
    public BarcodeIdentityDto? ActiveBarcode { get; set; }
    public bool CanManage { get; set; }
}
