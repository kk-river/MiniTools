using AppRunner;
using ConsoleAppFramework;

ConsoleApp.ConsoleAppBuilder builder = ConsoleApp.Create();
builder.Add<Commands>();
builder.Run(args);
