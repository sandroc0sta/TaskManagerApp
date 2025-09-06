# TaskManager Blazor App Report

## Introduction

This report provides a detailed guide to create a full-stack TaskManager application using Blazor WebAssembly and ASP.NET Core minimal API. Each step is explained with code snippets, file locations, and commands for dependencies.

## Index

* [Introduction](#introduction)
* [Prerequisites](#prerequisites)
* [API Setup](#api-setup)
* [Blazor Client Setup](#blazor-client-setup)
* [TaskService](#taskservice)
* [TaskManager Component](#taskmanager-component)
* [Styling (CSS)](#styling-css)
* [Running the App](#running-the-app)
* [Folder Structure](#folder-structure)
* [Optional Enhancements](#optional-enhancements)
* [Lessons Learned](#lessons-learned)

---

1. ## Prerequisites

Before starting, ensure you have the following installed:

### Required Software:

* [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
* [Visual Studio Code](https://code.visualstudio.com/) or Visual Studio
* SQLite (optional, as EF Core can create DB automatically)

### Commands to install dependencies

```bash
# Create API project
dotnet new web -n TaskManagerAPI

# Create Blazor WebAssembly client project
dotnet new blazorwasm -n TaskManagerClient

# Add EF Core and SQLite to API
cd TaskManagerAPI
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# Add EF Core tools (for migrations if needed)
dotnet add package Microsoft.EntityFrameworkCore.Tools

# Add JSON support (usually included)
dotnet add package Microsoft.AspNetCore.Mvc.NewtonsoftJson

# Ensure client has HTTP support (usually default)
cd ../TaskManagerClient
# HttpClient is included by default
```

---

2. ## API Setup

### 2.1 Project Creation

* Folder: `TaskManagerAPI`
* Create a new Web API project:

```bash
dotnet new web -n TaskManagerAPI
```

### 2.2 AppDbContext

* File: `Data/AppDbContext.cs`
* This class defines the database context for EF Core.

```csharp
using Microsoft.EntityFrameworkCore;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TaskItem> Tasks { get; set; }
    }
}
```

**Explanation:**

* `DbContext` manages the database connection and tables.
* `DbSet<TaskItem>` represents the `Tasks` table.

### 2.3 API Endpoints

* File: `Program.cs`
* Handles API configuration, endpoints, and database initialization.

```csharp
using Microsoft.EntityFrameworkCore;
using TaskManagerAPI.Data;
using TaskManagerAPI.Models;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=tasks.db"));

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5213")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure DB is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

// API Endpoints
app.MapGet("/tasks", async (AppDbContext db) => await db.Tasks.ToListAsync());

app.MapPost("/tasks", async (AppDbContext db, TaskItem task) =>
{
    db.Tasks.Add(task);
    await db.SaveChangesAsync();
    return Results.Created($"/tasks/{task.Id}", task);
});

app.MapPut("/tasks/{id}", async (int id, AppDbContext db, TaskItem updatedTask) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();
    task.Title = updatedTask.Title;
    task.IsDone = updatedTask.IsDone;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/tasks/{id}", async (int id, AppDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();
    db.Tasks.Remove(task);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
```

**Explanation:**

* Registers DbContext with SQLite.
* Sets up CORS to allow the Blazor client.
* Creates API endpoints for GET, POST, PUT, DELETE.
* Ensures database is created automatically.
* Enables Swagger for API documentation in development.

---

3. ## Blazor Client Setup

### 3.1 Project Creation

* Folder: `TaskManagerClient`
* Create a new Blazor WebAssembly project:

```bash
dotnet new blazorwasm -n TaskManagerClient
```

### 3.2 App.razor

* File: `App.razor`
* Handles routing in the Blazor app.

```razor
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <LayoutView Layout="@typeof(MainLayout)">
            <p>Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>
```

**Explanation:**

* `<Router>` manages page navigation.
* `<RouteView>` displays the current route's component.
* `<LayoutView>` wraps components with the specified layout.

---

4 ## TaskService

### 4.1 TaskService Class

* Folder: `Services/TaskService.cs`
* Provides API communication methods for the client.

```csharp
using System.Net.Http.Json;
using TaskManagerClient.Models;

namespace TaskManagerClient.Services
{
    public class TaskService
    {
        private readonly HttpClient _http;
        public TaskService(HttpClient http) => _http = http;

        public async Task<List<TaskItem>> GetTasks() =>
            await _http.GetFromJsonAsync<List<TaskItem>>("tasks") ?? new List<TaskItem>();

        public async Task<TaskItem?> AddTask(TaskItem task)
        {
            var response = await _http.PostAsJsonAsync("tasks", task);
            return await response.Content.ReadFromJsonAsync<TaskItem>();
        }

        public async Task UpdateTask(TaskItem task) =>
            await _http.PutAsJsonAsync($"tasks/{task.Id}", task);

        public async Task DeleteTask(int id) =>
            await _http.DeleteAsync($"tasks/{id}");
    }
}
```

**Explanation:**

* `GetTasks()` fetches all tasks.
* `AddTask(TaskItem)` posts a new task and returns the created object.
* `UpdateTask(TaskItem)` updates an existing task.
* `DeleteTask(int id)` deletes a task by ID.
* Uses `HttpClient` and `System.Net.Http.Json` for JSON serialization.

---

5. ## TaskManager Component

### 5.1 TaskManager.razor

* Folder: `Pages/TaskManager.razor`
* Main component for task management UI.

```razor
@page "/"
@inject TaskService TaskService
@using TaskManagerClient.Models
@using TaskManagerClient.Services

<div class="task-container">
    <h1>My Task Manager</h1>

    <div id="new-task-box">
        <input id="new-task-input" @bind="newTaskTitle" placeholder="New task" />
        <button id="new-task-button" @onclick="AddTask">Add</button>
    </div>

    <ul class="task-list">
        @foreach (var task in tasks)
        {
            <li class="task-item">
                <input type="checkbox" checked="@task.IsDone" @onchange="@(e => OnCheckboxChanged(task, e))" />
                <span class="task-title">@task.Title</span>
                <button class="delete-btn" @onclick="() => DeleteTask(task.Id)">Delete</button>
            </li>
        }
    </ul>
</div>

@code {
    private List<TaskItem> tasks = new();
    private string newTaskTitle = string.Empty;
    
    protected override async Task OnInitializedAsync()
    {
        tasks = await TaskService.GetTasks();
    }

    private async Task AddTask()
    {
        if(!string.IsNullOrWhiteSpace(newTaskTitle))
        {
            var task = new TaskItem {Title = newTaskTitle, IsDone = false};
            var createdTask = await TaskService.AddTask(task);
            tasks.Add(createdTask);
            newTaskTitle = string.Empty;
        }
    }

    private async Task UpdateTask(TaskItem task)
    {
        await TaskService.UpdateTask(task);
    }

    private async Task DeleteTask(int id)
    {
        await TaskService.DeleteTask(id);
        tasks.RemoveAll(t => t.Id == id);
    }

    private async Task OnCheckboxChanged(TaskItem task, ChangeEventArgs e)
    {
        task.IsDone = (bool)e.Value;
        await TaskService.UpdateTask(task);
    }
}
```

**Explanation:**

* `@inject` provides the `TaskService` instance.
* `AddTask()` adds a new task and clears the input.
* `DeleteTask()` removes a task locally and on the API.
* `OnCheckboxChanged()` updates the `IsDone` status.
* `tasks` list renders all tasks dynamically in the `<ul>`.

---

6. ## Styling CSS

### 6.1 site.css

* Folder: `wwwroot/css/site.css`
* Provides dark mode styling, hover effects, and input/button styling.

```css
/* DARK MODE BACKGROUND & FONT */
body {
    background-color: #1e1e2f;
    color: #e0e0e0;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    margin: 0;
    padding: 20px;
}

h1 {
    text-align: center;
    margin: 3rem;
}

/* TASK LIST CONTAINER */
.task-list {
    list-style: none;
    padding: 0;
    max-width: 600px;
    margin: auto;
}

/* TASK ITEM */
.task-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 10px 15px;
    margin-bottom: 10px;
    background-color: #2b2b3d;
    border-radius: 8px;
    transition: transform 0.2s, background-color 0.2s;
}

/* HOVER EFFECT */
.task-item:hover {
    transform: scale(1.02);
    background-color: #3a3a52;
}

/* TASK TITLE */
.task-title {
    flex-grow: 1;
    margin-left: 10px;
}

/* DELETE BUTTON */
.delete-btn {
    background-color: #ff4c4c;
    border: none;
    border-radius: 5px;
    color: #fff;
    padding: 5px 12px;
    cursor: pointer;
    transition: background-color 0.2s;
}
.delete-btn:hover {
    background-color: #ff1f1f;
}

/* CHECKBOX */
input[type="checkbox"] {
    width: 18px;
    height: 18px;
    cursor: pointer;
}

/* NEW TASK BOX */
#new-task-box {
    display: flex;
    justify-content: center;
    gap: 10px;
    max-width: 600px;
    margin: 0 auto 20px auto;
}

#new-task-input {
    flex: 1;
    max-width: 400px;
    padding: 8px 12px;
    border-radius: 6px;
    border: none;
    font-size: 16px;
    background-color: #2a2a2a;
    color: #fff;
}
#new-task-input::placeholder {
    color: #aaa;
}

#new-task-button {
    padding: 8px 15px;
    border: none;
    border-radius: 6px;
    background-color: #4caf50;
    color: white;
    cursor: pointer;
    transition: background-color 0.2s;
}
#new-task-button:hover {
    background-color: #45a049;
}
```

**Explanation:**

* Provides a consistent dark theme for the app.
* Styles tasks with hover animation for better UX.
* Centers the new task input and button.
* Styles delete buttons to stand out with red color.
* Ensures input and checkbox sizes are user-friendly.

---

7. ## Running the App

```bash
# Run API
cd TaskManagerAPI
dotnet run

# Run Client
cd ../TaskManagerClient
dotnet run
```

* Open browser at `https://localhost:5019` for API and `https://localhost:5213` for Blazor client.
* Use Swagger (`/swagger`) to inspect API endpoints.

---

8. ## Folder Structure

```
TaskManagerAPI/
  Data/
    AppDbContext.cs
  Models/
    TaskItem.cs
  Program.cs

TaskManagerClient/
  Pages/
    TaskManager.razor
  Services/
    TaskService.cs
  wwwroot/
    css/
      site.css
  App.razor
```

---

9. ## Optional Enhancements

* Use EF Core migrations instead of `EnsureCreated()`.
* Add user authentication.
* Add categories or filters for tasks.
* Persist completed tasks in separate lists or use local storage for offline support.
* Add toast notifications for task actions.

---

10. ## Lessons Learned

* Always ensure `@bind` and `@onchange` are not duplicated on the same element.
* Use proper string interpolation (`$"tasks/{task.Id}"`) to avoid `$tasks/%7Btask.Id%7D` errors.
* Centering elements with CSS requires understanding of `flex` and `margin: auto`.
* Dark mode and hover effects improve user experience but require careful color contrast.
* Restart components after fixing persistent errors to avoid cached states.
* Keep your folder structure organized for easier maintenance and navigation.
* Code snippets with explanations greatly help in debugging and understanding logic.

