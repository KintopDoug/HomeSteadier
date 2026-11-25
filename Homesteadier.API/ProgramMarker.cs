// Program.cs uses top-level statements, so the compiler generates an internal Program class. This makes it
// public so Homesteadier.Tests can host the real API in-process with WebApplicationFactory<Program>.
// It adds no behaviour; do not put anything here.
public partial class Program;
