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