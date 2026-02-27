// Simple test app for debugging
int x = 10;
int y = 20;
int sum = Add(x, y);         // line 4 — good breakpoint target

Console.WriteLine($"Sum: {sum}");
Console.WriteLine($"Product: {x * y}");

string message = Greet("World");
Console.WriteLine(message);   // line 10

for (int i = 0; i < 3; i++)  // line 12 — loop breakpoint target
{
    int squared = i * i;
    Console.WriteLine($"  {i}^2 = {squared}");
}

Console.WriteLine("Done.");

static int Add(int a, int b)  // line 19
{
    int result = a + b;       // line 21
    return result;            // line 22
}

static string Greet(string name)  // line 25
{
    string greeting = $"Hello, {name}!";
    return greeting;
}
