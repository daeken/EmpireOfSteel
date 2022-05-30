namespace Common;

[AttributeUsage(AttributeTargets.Class)]
public class LibraryAttribute : Attribute {
	public readonly string Name;

	public LibraryAttribute(string name) => Name = name;
}