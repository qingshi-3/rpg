using StrategicMapPresentationRegression;

string projectRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

PresentationRegressionCases.Run("production visual bindings are complete and byte-identical", () =>
    PresentationRegressionCases.ProductionVisualBindingsAreComplete(projectRoot));
PresentationRegressionCases.Run("production visual imports are reproducible and collision-free", () =>
    PresentationRegressionCases.ProductionVisualImportsAreReproducible(projectRoot));
PresentationRegressionCases.Run("world queries resolve canonical chunks and visible rectangles", () =>
    PresentationRegressionCases.WorldQueriesResolveCanonicalChunks(projectRoot));
PresentationRegressionCases.Run("threaded residency scheduling is bounded and failure-stable", () =>
    PresentationRegressionCases.ThreadedResidencySchedulingIsBounded(projectRoot));
PresentationRegressionCases.Run("derived region inputs match canonical geography", () =>
    PresentationRegressionCases.RegionInputsMatchCanonicalGeography(projectRoot));
PresentationRegressionCases.Run("campaign region treatment consumes the read-only Strategic Management port", () =>
    PresentationRegressionCases.CampaignRegionTreatmentConsumesReadOnlyStrategicManagementPort(projectRoot));
PresentationRegressionCases.Run("authored production scene is isolated and bounded", () =>
    PresentationRegressionCases.AuthoredProductionSceneIsIsolated(projectRoot));
PresentationRegressionCases.Run("production source excludes forbidden dependencies", () =>
    PresentationRegressionCases.ProductionSourceExcludesForbiddenDependencies(projectRoot));
PresentationRegressionCases.Run("both published packages load through the same contracts", () =>
    PresentationRegressionCases.BothPublishedPackagesLoadThroughSameContracts(projectRoot));

Console.WriteLine("Strategic map presentation regression tests passed.");
