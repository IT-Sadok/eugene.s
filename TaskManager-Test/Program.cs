var taskManager = new TaskManagementSystem();

taskManager.AddDeveloper(new Developer(1, "Dima"));
taskManager.AddDeveloper(new Developer(2, "Eugene"));
taskManager.AddDeveloper(new Developer(3, "Grisha"));
CreateTasks(taskManager);
AssignTasks(taskManager);
StartTask(taskManager);


bool running = true;
while (running)
{
    Console.Clear();
    Console.WriteLine("TaskManager");
    Console.WriteLine("1. Show all tasks");
    Console.WriteLine("2. Start asynchronous simulation of task execution");
    Console.WriteLine("0. Exit");
    Console.Write("\nChoose an option: ");

    if (int.TryParse(Console.ReadLine(), out var choice))
    {
        switch (choice)
        {
            case 1:
                ShowAllTasks(taskManager);
                break;
            case 2:
                await RunAsyncSimulation(taskManager);
                break;
            case 0:
                running = false;
                break;
            default:
                Console.WriteLine("Wrong option, choose number from list");
                Console.ReadKey();
                break;
        }
    }
    else
    {
        Console.WriteLine("Please provide correct number from the options list");
        Console.ReadKey();
    }
}


static void CreateTasks(TaskManagementSystem taskManager)
{
    var tasks = new List<DevelopmentTask>()
    {
        new()
        {
            Id = 1,
            Title = "Bikes CRUD",
            Description = "Create apis for bike creation, deletion, update and list",
            Priority = Priority.Medium,
            Status = TaskStatus.Created,
            CreatedDate = DateTime.Now
        },
        new()
        {
            Id = 2,
            Title = "Create User",
            Description = "Create apis for user creation",
            Priority = Priority.Low,
            Status = TaskStatus.Created,
            CreatedDate = DateTime.Now
        },
        new()
        {
            Id = 3,
            Title = "Bikes Assignment",
            Description = "Create apis for bike assignment to te user",
            Priority = Priority.High,
            Status = TaskStatus.Created,
            CreatedDate = DateTime.Now
        }
    };
    taskManager.AddTaskRange(tasks);
}

static void AssignTasks(TaskManagementSystem taskManager)
{
    var assignments = new[]
    {
        new AssignTaskRequest { DeveloperId = 1, TaskId = 1 },
        new AssignTaskRequest { DeveloperId = 2, TaskId = 2 },
        new AssignTaskRequest { DeveloperId = 3, TaskId = 3 }
    };

    var tasks = assignments
        .Select(req => Task.Run(() => taskManager.AssignTaskToDeveloper(req)))
        .ToArray<Task>();

    Task.WaitAll(tasks);
}


static void StartTask(TaskManagementSystem taskManager)
{
    var tasks = taskManager.GetAssignedTasks()
        .Select(task => Task.Run(() => taskManager.StartTask(task.Id)))
        .ToArray<Task>();

    Task.WaitAll(tasks);
}

static void ShowAllTasks(TaskManagementSystem taskManager)
{
    Console.Clear();
    Console.WriteLine("=== All tasks ===");

    var tasks = taskManager.GetAllTasks();
    DisplayTasks(tasks);

    Console.WriteLine("Enter any key to continue ..");
    Console.ReadKey();
}

static void DisplayTasks(IEnumerable<DevelopmentTask> tasks)
{
    Console.WriteLine(new string('-', 100));
    Console.WriteLine($"{"ID",-5}{"Name",-20}{"Priority",-12}{"Status",-15}{"Developer",-15}{"Created At",-20}");
    Console.WriteLine(new string('-', 100));

    foreach (var task in tasks)
    {
        string developerName = task.AssignedDeveloper != null ? task.AssignedDeveloper.Name : "Not Assigned";
        Console.WriteLine(
            $"{task.Id,-5}{task.Title,-20}{task.Priority,-12}{task.Status,-15}{developerName,-15}{task.CreatedDate,-20:dd/MM/yyyy HH:mm}");
    }

    Console.WriteLine(new string('-', 100));
}

async Task RunAsyncSimulation(TaskManagementSystem taskManager)
{
    Console.Clear();
    Console.WriteLine("=== Async simulation ===");

    var activeTasks = taskManager.GetActiveTasks().ToList();
    if (activeTasks.Count == 0)
    {
        Console.WriteLine("No active tasks for simulation.");
        Console.WriteLine("Enter any key to continue...");
        Console.ReadKey();
        return;
    }

    Console.WriteLine($"Starting async processing of {activeTasks.Count} tasks...");

    using (var semaphore = new SemaphoreSlim(2)) // Only 2 tasks can run simultaneously
    {
        var simulationTasks = activeTasks.Select(async task =>
        {
            await semaphore.WaitAsync();
            try
            {
                var duration = GetTaskDuration(task.Priority);
                Console.WriteLine(
                    $"{DateTime.Now:HH:mm:ss} - Task is started '{task.Title}' (ID: {task.Id}) - expected execution time: {duration} seconds");

                // Simulate work
                await Task.Delay(duration * 1000);

                taskManager.CompleteTask(task.Id);
                Console.WriteLine(
                    $"{DateTime.Now:HH:mm:ss} - Task completed '{task.Title}' (ID: {task.Id})");
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(simulationTasks);
    }

    Console.WriteLine("All tasks completed!");
    Console.WriteLine("Enter any key to continue...");
    Console.ReadKey();
}

static int GetTaskDuration(Priority priority)
{
    return priority switch
    {
        Priority.Low => 5,
        Priority.Medium => 3,
        Priority.High => 2,
        _ => 4
    };
}


public enum Priority
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum TaskStatus
{
    Created,
    Assigned,
    InProgress,
    Completed
}

public class DevelopmentTask
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public Priority Priority { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Developer AssignedDeveloper { get; set; }
}

public class Developer
{
    public int Id { get; init; }
    public string Name { get; set; }

    public Developer(int id, string name)
    {
        Id = id;
        Name = name;
    }
}


public class AssignTaskRequest()
{
    public int TaskId { get; init; }
    public int DeveloperId { get; init; }
}

public class TaskManagementSystem
{
    private readonly SemaphoreSlim _semaphore = new(1);
    private List<DevelopmentTask> _tasks = [];
    private List<Developer> _developers = [];

    private Dictionary<int, Queue<DevelopmentTask>>
        _developerWorkQueues = new();

    private Stack<DevelopmentTask> _completedTasks = new();

    public void AddDeveloper(Developer developer)
    {
        _developers.Add(developer);
        _developerWorkQueues[developer.Id] = new Queue<DevelopmentTask>();
    }

    public void AddTaskRange(List<DevelopmentTask> tasks) =>
        _tasks.AddRange(tasks);

    public bool AssignTaskToDeveloper(AssignTaskRequest assignTaskRequest)
    {
        _semaphore.Wait();
        try
        {
            var task = _tasks.FirstOrDefault(t => t.Id == assignTaskRequest.TaskId);
            var developer = _developers.FirstOrDefault(d => d.Id == assignTaskRequest.DeveloperId);

            if (task == null || developer == null) return false;
            task.AssignedDeveloper = developer;
            task.Status = TaskStatus.Assigned;
            _developerWorkQueues[developer.Id].Enqueue(task);
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public bool StartTask(int taskId)
    {
        _semaphore.Wait();
        try
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task is not { Status: TaskStatus.Assigned }) return false;
            task.Status = TaskStatus.InProgress;
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public bool CompleteTask(int taskId)
    {
        _semaphore.Wait();
        try
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task is not { Status: TaskStatus.InProgress }) return false;
            task.Status = TaskStatus.Completed;
            task.CompletedDate = DateTime.Now;

            _completedTasks.Push(task);

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public IEnumerable<DevelopmentTask> GetAllTasks() => _tasks;

    public IEnumerable<DevelopmentTask> GetActiveTasks() =>
        _tasks.Where(t => t.Status == TaskStatus.InProgress)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedDate);

    public IEnumerable<DevelopmentTask> GetAssignedTasks() =>
        _tasks.Where(t => t.Status == TaskStatus.Assigned);
}