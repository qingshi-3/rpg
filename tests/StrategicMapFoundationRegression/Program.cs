using StrategicMapFoundationRegression;

string projectRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

FoundationRegressionCases.Run("canonical geography and chunk manifest load", () =>
    FoundationRegressionCases.CanonicalGeographyAndChunkManifestLoad(projectRoot));
FoundationRegressionCases.Run("canonical province and city geometry contract", () =>
    FoundationRegressionCases.CanonicalProvinceAndCityGeometryContract(projectRoot));
FoundationRegressionCases.Run("validator rejects broken identity and coordinate contracts", () =>
    FoundationRegressionCases.ValidatorRejectsBrokenContracts(projectRoot));
FoundationRegressionCases.Run("package loader rejects tampering and cross-revision references", () =>
    FoundationRegressionCases.PackageLoaderRejectsTamperingAndCrossRevisionReferences(projectRoot));
FoundationRegressionCases.Run("module source stays within pure foundation layers", () =>
    FoundationRegressionCases.ModuleSourceStaysWithinPureFoundationLayers(projectRoot));

Console.WriteLine("Strategic map foundation regression tests passed.");
