namespace Common; 

[AttributeUsage(AttributeTargets.Method)]
public class ExportAttribute : Attribute {
	public readonly string EncodedId;

	public ExportAttribute(string encodedId) => EncodedId = encodedId;
}