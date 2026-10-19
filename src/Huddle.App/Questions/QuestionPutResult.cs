namespace Agency.Huddle.App.Questions;

/// <summary>The outcome of <see cref="QuestionStore.TryPut"/> against Questions spec §8.1.</summary>
internal enum QuestionPutResult
{
    /// <summary>No card was waiting in the Room; the new one is stored.</summary>
    Stored,

    /// <summary>The same Agent's earlier card in this Room was replaced.</summary>
    Replaced,

    /// <summary>Another Agent's card is already waiting in this Room; the new one was refused.</summary>
    Refused,
}
