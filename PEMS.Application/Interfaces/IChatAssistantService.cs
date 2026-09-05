using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IChatAssistantService
{
    Task<List<ChatMessageDto>> GetHistoryAsync(int userId);
    Task<string> AskAsync(int userId, string question);
    Task ClearHistoryAsync(int userId);
}
