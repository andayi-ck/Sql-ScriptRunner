using BCrypt.Net;

Console.Title = "Kabarak — Password Hash Generator";
Console.WriteLine("===========================================");
Console.WriteLine("  Kabarak Script Runner — Password Hasher");
Console.WriteLine("===========================================");
Console.WriteLine();
Console.WriteLine("This tool generates BCrypt hashes for user passwords.");
Console.WriteLine("Paste the generated hash into SSMS to INSERT or UPDATE a user.");
Console.WriteLine("Press ENTER on an empty password to quit.");
Console.WriteLine();

while (true)
{
    Console.WriteLine("-------------------------------------------");
    Console.Write("Enter password to hash: ");
    var password = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(password))
    {
        Console.WriteLine("Bye.");
        break;
    }

    var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

    Console.WriteLine();
    Console.WriteLine("Hash:");
    Console.WriteLine(hash);
    Console.WriteLine();
    Console.WriteLine($"Length: {hash.Length} (should be 60)");
    Console.WriteLine();
}