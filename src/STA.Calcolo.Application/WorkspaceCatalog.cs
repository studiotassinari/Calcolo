namespace STA.Calcolo.Application;

public static class WorkspaceCatalog { public static IReadOnlyList<ICalculationWorkspace> Create() => [new ShearWorkspace(), new ConcreteWorkspace(), new SteelWorkspace(), new SnowWorkspace(), new WindWorkspace(), new SeismicWorkspace(), new SectionWorkspace(), new TendonWorkspace(), new HistoricalFootingWorkspace(), new HistoricalFootingWorkspace(true)]; }