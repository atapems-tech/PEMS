namespace PEMS.Application.AI.OCR;

public interface IOcrService
{
    Task<string> RecognizeAsync(byte[] imageBytes);
    Task<string> RecognizeFileAsync(string imagePath);
}
