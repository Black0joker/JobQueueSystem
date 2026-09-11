using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;

namespace JobQueue.Domain.Jobs;

/// <summary>
/// The single source of truth for allowed job lifecycle transitions (phase 20).
/// All status changes must go through <see cref="Job.TransitionTo"/>, which enforces
/// this table, so no code path can move a job into an arbitrary state.
/// </summary>
/// <remarks>
/// Allowed transitions:
/// Scheduled   -> Pending (dispatcher, phase 17), Cancelled
/// Pending     -> Processing (worker claim), Cancelled
/// Processing  -> Completed, Failed, DeadLettered, Cancelled, Pending (stuck-job recovery reset, phase 14)
/// Failed      -> Pending (manual retry, phase 16)
/// DeadLettered-> Pending (manual retry, phase 16)
/// Completed, Cancelled are terminal and have no outgoing transitions.
/// </remarks>
public static class JobStatusTransitions
{
    private static readonly IReadOnlyDictionary<JobStatus, IReadOnlySet<JobStatus>> AllowedTransitions =
        new Dictionary<JobStatus, IReadOnlySet<JobStatus>>
        {
            [JobStatus.Scheduled] = new HashSet<JobStatus>
            {
                JobStatus.Pending,
                JobStatus.Cancelled
            },
            [JobStatus.Pending] = new HashSet<JobStatus>
            {
                JobStatus.Processing,
                JobStatus.Cancelled
            },
            [JobStatus.Processing] = new HashSet<JobStatus>
            {
                JobStatus.Completed,
                JobStatus.Failed,
                JobStatus.DeadLettered,
                JobStatus.Cancelled,
                JobStatus.Pending // stuck-job recovery resets and re-dispatches
            },
            [JobStatus.Failed] = new HashSet<JobStatus>
            {
                JobStatus.Pending // manual retry
            },
            [JobStatus.DeadLettered] = new HashSet<JobStatus>
            {
                JobStatus.Pending // manual retry
            },
            [JobStatus.Completed] = new HashSet<JobStatus>(),
            [JobStatus.Cancelled] = new HashSet<JobStatus>()
        };

    /// <summary>Whether moving from <paramref name="from"/> to <paramref name="to"/> is allowed.</summary>
    public static bool CanTransition(JobStatus from, JobStatus to)
        => AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// Throws <see cref="InvalidJobTransitionException"/> when the transition is not allowed.
    /// </summary>
    public static void EnsureValid(JobStatus from, JobStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidJobTransitionException(
                $"Invalid job state transition from '{from}' to '{to}'. " +
                $"Allowed transitions from '{from}': {DescribeAllowed(from)}.");
        }
    }

    private static string DescribeAllowed(JobStatus from)
        => AllowedTransitions.TryGetValue(from, out var targets) && targets.Count > 0
            ? string.Join(", ", targets)
            : "none (terminal state)";
}
