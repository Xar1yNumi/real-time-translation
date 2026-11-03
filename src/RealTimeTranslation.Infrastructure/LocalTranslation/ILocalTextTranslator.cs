namespace RealTimeTranslation.Infrastructure.LocalTranslation;

public interface ILocalTextTranslator
{
    Task<string> TranslateAsync(string text, string sourceCode, CancellationToken cancellationToken);
}
