using StrategicRegionPreviewRegression;

string projectRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

PreviewRegressionCases.Run(
    "preview geography defines irregular five and six region topologies",
    () => PreviewRegressionCases.PreviewGeographyDefinesIrregularFiveAndSixRegionTopologies(projectRoot));
PreviewRegressionCases.Run(
    "preview scene is independently runnable and isolated",
    () => PreviewRegressionCases.PreviewSceneIsIndependentlyRunnableAndIsolated(projectRoot));
PreviewRegressionCases.Run(
    "preview uses reusable authored scenes and presentation resources",
    () => PreviewRegressionCases.PreviewUsesReusableAuthoredScenesAndPresentationResources(projectRoot));
PreviewRegressionCases.Run(
    "preview loader preserves canonical coordinates",
    () => PreviewRegressionCases.PreviewLoaderPreservesCanonicalCoordinates(projectRoot));
PreviewRegressionCases.Run(
    "preview uses chunk mask overlays without region polygons",
    () => PreviewRegressionCases.PreviewUsesChunkMaskOverlaysWithoutRegionPolygons(projectRoot));
PreviewRegressionCases.Run(
    "preview uses uncapped mask metadata lookup for faction and city membership",
    () => PreviewRegressionCases.PreviewUsesMaskMetadataLookup(projectRoot));

Console.WriteLine("Strategic region preview regression tests passed.");
