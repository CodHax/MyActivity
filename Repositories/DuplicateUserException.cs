namespace MyActivity.Repositories;

public class DuplicateUserException : Exception
{
    public DuplicateUserException() : base("Employee ID or email already exists.") { }
}
