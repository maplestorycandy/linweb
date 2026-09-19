namespace IdleLineage.Data;

public sealed record WorldTravelRoute(string Id, string Mode, string FromMapKey, string FromLandmarkId, string ToMapKey, string ToLandmarkId, bool RequiresNpcInteraction);
