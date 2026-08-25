using Microsoft.EntityFrameworkCore;
using HubPessoal.Application;
using HubPessoal.Application.Interfaces;
using HubPessoal.Domain.Entities;
using HubPessoal.Infrastructure;
using HubPessoal.Infrastructure.Data;
using HubPessoal.Api.Middlewares;
using HubPessoal.Api.Contracts;
using HubPessoal.Api.Contracts.Notes;
using HubPessoal.Api.Contracts.Folders;
using HubPessoal.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using HubPessoal.Api.Filters;
using HubPessoal.Application.Options;
using HubPessoal.Api.Contracts.Sync;
using HubPessoal.Application.Models;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "database");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

const string CorsPolicy = "frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    policy
    .WithOrigins(
        "http://localhost:3000",
        "https://frontend-hub-pessoal.vercel.app")
    .AllowAnyHeader()
    .AllowAnyMethod());
});

builder.Services.AddApplication();

builder.Services.AddScoped<IValidator<CreateNoteRequest>, CreateNoteRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateNoteRequest>, UpdateNoteRequestValidator>();
builder.Services.AddScoped<IValidator<CreateFolderRequest>, CreateFolderRequestValidator>();
builder.Services.AddScoped<IValidator<RenameFolderRequest>, RenameFolderRequestValidator>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Jwt:Key not configured."))),
        };
    });

builder.Services.AddAuthorization();

builder.Services.Configure<GitOptions>(
    builder.Configuration.GetSection(GitOptions.SectionName));

var app = builder.Build();

app.Services.GetRequiredService<IOptions<GitOptions>>().Value.EnsureConfigured();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
    .GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any())
    {
        var seedUsername = app.Configuration["Auth:SeedUsername"]
            ?? throw new InvalidOperationException("Auth:SeedUsername not configured.");
        var seedPassword = app.Configuration["Auth:SeedPassword"]
            ?? throw new InvalidOperationException("Auth:SeedPassword not configured.");

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Users.Add(new User(seedUsername, passwordHasher.Hash(seedPassword)));
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
}

// app.MapGet("/", () => "Hub Pessoal API - v1.0.0");
app.MapHealthChecks("/health");

app.MapPost("/auth/login", async(LoginRequest request, AuthService authService) =>
{
    var token = await authService.LoginAsync(request.Username, request.Password);
    return token is null
        ? Results.Unauthorized()
        : Results.Ok(new LoginResponse(token));
});

app.MapGet("auth/me", (ClaimsPrincipal user) =>
{
    var username = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
    return Results.Ok(new { username });
}).RequireAuthorization();
// app.MapGet("/erro-teste", () =>
// {
//     throw new InvalidOperationException("Erro de teste do middleware.");
// });

var folders = app.MapGroup("/folders").RequireAuthorization();

folders.MapGet("/", async (NoteFolderService folderService) =>
{
    var allFolders = await folderService.GetAllAsync();
    return Results.Ok(allFolders.Select(FolderResponse.FromEntity));
});

folders.MapPost("/", async (CreateFolderRequest request, NoteFolderService folderService) =>
{
    var (result, folder) = await folderService.CreateAsync(request.Name, request.ParentFolderId);
    return result switch
    {
        CreateFolderResult.Success => Results.Created($"/folders/{folder!.Id}", FolderResponse.FromEntity(folder)),
        CreateFolderResult.ParentNotFound => Results.BadRequest("ParentFolderId does not exist."),
        CreateFolderResult.DuplicateName => Results.Conflict("A folder with this name already exists in the same parent."),
        _ => Results.Problem()
    };
}).AddEndpointFilter<ValidationFilter<CreateFolderRequest>>();

folders.MapPut("/{id:guid}", async (Guid id, RenameFolderRequest request, NoteFolderService folderService) =>
{
    var (result, folder) = await folderService.RenameAsync(id, request.Name);
    return result switch
    {
        RenameFolderResult.Success => Results.Ok(FolderResponse.FromEntity(folder!)),
        RenameFolderResult.NotFound => Results.NotFound(),
        RenameFolderResult.DuplicateName => Results.Conflict("A folder with this name already exists in the same parent."),
        _ => Results.Problem()
    };
}).AddEndpointFilter<ValidationFilter<RenameFolderRequest>>();

folders.MapPatch("/{id:guid}/move", async (Guid id, MoveFolderRequest request, NoteFolderService folderService) =>
{
    var result = await folderService.MoveAsync(id, request.ParentFolderId);
    return result switch
    {
        MoveFolderResult.Success => Results.NoContent(),
        MoveFolderResult.NotFound => Results.NotFound(),
        MoveFolderResult.ParentNotFound => Results.BadRequest("ParentFolderId does not exist."),
        MoveFolderResult.InvalidParent => Results.Conflict("Cannot move a folder into itself or one of its own descendants."),
        MoveFolderResult.DuplicateName => Results.Conflict("A folder with this name already exists in the target parent."),
        _ => Results.Problem()
    };
}).AddEndpointFilter<ValidationFilter<MoveFolderRequest>>();

folders.MapDelete("/{id:guid}", async (Guid id, NoteFolderService folderService) =>
{
    var result = await folderService.DeleteAsync(id);
    return result switch
    {
        DeleteFolderResult.Success => Results.NoContent(),
        DeleteFolderResult.NotFound => Results.NotFound(),
        DeleteFolderResult.NotEmpty => Results.Conflict("Folder is not empty. Move or delete its contents first."),
        _ => Results.Problem()
    };
});

var notes = app.MapGroup("/notes").RequireAuthorization();

notes.MapGet("/", async (NoteService noteService) =>
{
    var allNotes = await noteService.GetAllAsync();
    return Results.Ok(allNotes.Select(NoteResponse.FromEntity));
});

notes.MapGet("tree", async (NoteService noteService) =>
{
    var tree = await noteService.GetTreeAsync();
    return Results.Ok(tree);
});

notes.MapGet("/{id:guid}", async (Guid id, NoteService noteService) =>
{
    var note = await noteService.GetByIdAsync(id);
    return note is null ? Results.NotFound() : Results.Ok(NoteResponse.FromEntity(note));
});

notes.MapPost("/", async (CreateNoteRequest request, NoteService noteService) =>
{
    var (result, note) = await noteService.CreateAsync(request.Title, request.Content, request.FolderId, request.Tags ?? new List<string>());
    return result switch
    {
        CreateNoteResult.Success => Results.Created($"/notes/{note!.Id}", NoteResponse.FromEntity(note)),
        CreateNoteResult.FolderNotFound => Results.BadRequest("FolderId does not exist."),
        CreateNoteResult.DuplicateTitle => Results.Conflict("A note with this title already exists in the same folder."),
        _ => Results.Problem()
    };
}).AddEndpointFilter<ValidationFilter<CreateNoteRequest>>();

notes.MapPut("/{id:guid}", async (Guid id, UpdateNoteRequest request, NoteService noteService) =>
{
    var (result, note) = await noteService.UpdateAsync(id, request.Title, request.Content, request.Tags ?? new List<string>());
    return result switch
    {
        UpdateNoteResult.Success => Results.Ok(NoteResponse.FromEntity(note!)),
        UpdateNoteResult.NotFound => Results.NotFound(),
        UpdateNoteResult.DuplicateTitle => Results.Conflict("A note with this title already exists in the same folder."),
        _ => Results.Problem()
    };
}).AddEndpointFilter<ValidationFilter<UpdateNoteRequest>>();

notes.MapPatch("/{id:guid}/move", async (Guid id, MoveNoteRequest request, NoteService noteService) =>
{
    var result = await noteService.MoveAsync(id, request.FolderId);
    return result switch
    {
        MoveNoteResult.Success => Results.NoContent(),
        MoveNoteResult.NotFound => Results.NotFound(),
        MoveNoteResult.FolderNotFound => Results.BadRequest("FolderId does not exist."),
        MoveNoteResult.DuplicateTitle => Results.Conflict("A note with this title already exists in the target folder."),
        _ => Results.Problem()
    };
});

notes.MapDelete("/{id:guid}", async (Guid id, NoteService noteService) =>
{
    var deleted = await noteService.DeleteAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

notes.MapPatch("/{id:guid}/pin", async (Guid id, NoteService noteService) =>
{
    var note = await noteService.TogglePinAsync(id);
    return note is null ? Results.NotFound() : Results.Ok(NoteResponse.FromEntity(note));
});

notes.MapPost("/{id:guid}/duplicate", async (Guid id, NoteService noteService) =>
{
    var copy = await noteService.DuplicateAsync(id);
    return copy is null ? Results.NotFound() : Results.Created($"/notes/{copy.Id}", NoteResponse.FromEntity(copy));
});

notes.MapGet("/{id:guid}/export", async (Guid id, NoteService noteService) =>
{
    var note = await noteService.GetByIdAsync(id);
    if (note is null)
    {
        return Results.NotFound();
    }

    var bytes = System.Text.Encoding.UTF8.GetBytes(note.Content);
    return Results.File(bytes, "text/markdown", $"{note.Title}.md");
});

var sync = app.MapGroup("/sync").RequireAuthorization();

sync.MapGet("/status", async (SyncService syncService) =>
{
    var plan = await syncService.PreviewAsync();
    return Results.Ok(new SyncStatusResponse(
        plan.State.ToString(), plan.FilesChanged, plan.IncomingCommits));
});

sync.MapPost("/preview", async (SyncService syncService) =>
{
    var plan = await syncService.PreviewAsync();
    return Results.Ok(SyncPlanResponse.FromPlan(plan));
});

sync.MapPost("/apply", async (ApplySyncRequest request, SyncService syncService) =>
{
    var outcome = await syncService.ApplyAsync(request.Fingerprint);

    return outcome.Result switch
    {
        SyncApplyResult.Success => Results.Ok(new { commitHash = outcome.CommitHash }),
        SyncApplyResult.NothingToDo => Results.NoContent(),
        SyncApplyResult.PlanExpired => Results.Conflict(
            new ApplySyncErrorResponse("PlanExpired", outcome.Detail ?? string.Empty)),
        SyncApplyResult.Conflict => Results.Conflict(
            new ApplySyncErrorResponse("Conflict", outcome.Detail ?? string.Empty)),
        SyncApplyResult.GitFailure => Results.Problem(outcome.Detail),
        _ => Results.Problem()
    };
});

sync.MapGet("/history", async (ISyncCommitRepository commitRepository) =>
{
    var commits = await commitRepository.GetRecentAsync(50);

    return Results.Ok(commits.Select(c => new SyncCommitSummaryResponse(
        c.CommitHash, c.Message, c.CommittedAt, c.FilesChanged, c.Insertions, c.Deletions)));
});

sync.MapGet("/history/{hash}", async (string hash, ISyncCommitRepository commitRepository) =>
{
    var commit = await commitRepository.GetByHashAsync(hash);
    return commit is null ? Results.NotFound() : Results.Ok(SyncCommitDetailResponse.FromEntity(commit));
});

app.Run();
