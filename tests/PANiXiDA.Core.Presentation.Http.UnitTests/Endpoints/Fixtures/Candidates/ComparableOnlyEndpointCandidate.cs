namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Candidates;

public sealed class ComparableOnlyEndpointCandidate : IComparable<ComparableOnlyEndpointCandidate>
{
    public int CompareTo(ComparableOnlyEndpointCandidate? other)
    {
        return 0;
    }
}
