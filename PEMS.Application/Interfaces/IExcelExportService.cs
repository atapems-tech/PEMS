using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IExcelExportService
{
    byte[] ExportDocumentRegister(List<EngineeringDocumentDto> documents);
}
