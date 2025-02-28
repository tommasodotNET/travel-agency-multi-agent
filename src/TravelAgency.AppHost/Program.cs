var builder = DistributedApplication.CreateBuilder(args);

var openai = builder.AddConnectionString("openAiConnectionName");

// Add a SQL Server container
var sqlPassword = builder.AddParameter("sql-password");
var sqlServer = builder
    .AddSqlServer("sql", sqlPassword);
var sqlDatabase = sqlServer.AddDatabase("Agency");

sqlServer.WithLifetime(ContainerLifetime.Persistent);

// Populate the database with the schema and data
sqlServer
    .WithBindMount("./sql-server", target: "/usr/config")
    .WithBindMount("../../database", target: "/docker-entrypoint-initdb.d")
    .WithEntrypoint("/usr/config/entrypoint.sh");

var dab = builder.AddExecutable("dab", "dab", "../../dab/", "start")
    .WithReference(sqlDatabase)
    .WithHttpEndpoint(targetPort: 5000)
    .WaitFor(sqlServer)
    .WithOtlpExporter();
var dabEndpoint = dab.GetEndpoint("http");

var offeringsExpert = builder.AddProject<Projects.TravelAgency_OfferingsExpert>("offerings-expert")
    .WithReference(dabEndpoint)
    .WithReference(openai);

var tripPlanner = builder.AddUvApp("trip-planner", "../trip-planner", "trip-planner")
    .WithHttpEndpoint(env: "PORT");

var processOrchestrator = builder.AddProject<Projects.TravelAgency_ProcessOrchestrator>("process-orchestrator")
    .WithReference(offeringsExpert)
    .WithReference(tripPlanner);

var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithNpmPackageInstallation()
    .WithReference(processOrchestrator)
    .WithHttpEndpoint(env: "PORT");
    
builder.Build().Run();
