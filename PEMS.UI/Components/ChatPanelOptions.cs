using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

// SCRUM-778: the chat panel. A message log that reads the history from a data source, a box that posts the question to a stored action and a clear button that posts to another one; the page that is the chat
// assistant is a definition that names the three keys, so no controller, view or script belongs to one assistant. pems-chat.js reads the history (GET /d/{source}), posts (POST /a/{action}, JSON, antiforgery
// token of the page) and shows the history again, where the answer of the assistant has been stored by the action.

/// <summary>The names of the properties of a history row that the panel shows.</summary>
public sealed record ChatFields
{
    public string Role { get; init; } = "role";
    public string Content { get; init; } = "content";
    public string Time { get; init; } = "createdDate";
}

/// <summary>The texts of the chat panel (inputs, ADR-0008); the defaults are the platform's two languages.</summary>
public sealed record ChatPanelTexts
{
    public TranslatableText Placeholder { get; init; } = TranslatableText.Of(("en", "Type a question..."), ("fa", "پرسش خود را بنویسید..."));
    public TranslatableText Label { get; init; } = TranslatableText.Of(("en", "Question"), ("fa", "پرسش"));
    public TranslatableText Send { get; init; } = TranslatableText.Of(("en", "Send"), ("fa", "ارسال"));
    public TranslatableText Clear { get; init; } = TranslatableText.Of(("en", "Clear"), ("fa", "پاک کردن"));
    public TranslatableText Empty { get; init; } = TranslatableText.Of(("en", "No messages yet."), ("fa", "هنوز پیامی نیست."));
    public TranslatableText Loading { get; init; } = TranslatableText.Of(("en", "Loading..."), ("fa", "در حال بارگذاری..."));
    public TranslatableText Thinking { get; init; } = TranslatableText.Of(("en", "Thinking..."), ("fa", "در حال پاسخ..."));
    public TranslatableText Error { get; init; } = TranslatableText.Of(("en", "The message could not be sent."), ("fa", "پیام ارسال نشد."));
    public TranslatableText You { get; init; } = TranslatableText.Of(("en", "You"), ("fa", "شما"));
    public TranslatableText Assistant { get; init; } = TranslatableText.Of(("en", "Assistant"), ("fa", "دستیار"));
}

/// <summary>Inputs of <see cref="PemsChatPanel"/>.</summary>
public sealed record ChatPanelOptions : FrameOptions
{
    public required string Id { get; init; }
    /// <summary>The key of the data source that lists the messages of the user (<c>/d/{key}</c>, <c>{ items: [...] }</c>).</summary>
    public required string HistorySource { get; init; }
    /// <summary>The key of the action that takes the question and stores the answer (<c>/a/{key}</c>).</summary>
    public required string AskAction { get; init; }
    /// <summary>The name of the value the ask action receives (the question).</summary>
    public string AskField { get; init; } = "message";
    /// <summary>The key of the action that clears the history; without it there is no clear button.</summary>
    public string? ClearAction { get; init; }
    public ChatFields Fields { get; init; } = new();
    /// <summary>The value of the role field that marks a message of the user (compared without case); every other role is the assistant.</summary>
    public string UserRole { get; init; } = "User";
    public int MaxLength { get; init; } = 2000;
    public int Height { get; init; } = 480;
    public ChatPanelTexts Texts { get; init; } = new();
}
