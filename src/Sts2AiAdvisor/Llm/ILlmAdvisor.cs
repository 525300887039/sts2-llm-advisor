using System.Threading;
using System.Threading.Tasks;

namespace Sts2AiAdvisor.Llm;

/// <summary>Provider-agnostic card-advice service. The HTTP call runs OFF the game thread.</summary>
public interface ILlmAdvisor
{
    Task<AdviceResult> GetAdviceAsync(AdviceRequest req, CancellationToken ct);
}
