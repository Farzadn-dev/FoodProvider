using System.Diagnostics.CodeAnalysis;

public sealed class RabbitRequestDto<T>
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required T Data { get; set; }

    [SetsRequiredMembers]
    public RabbitRequestDto(T data)
    {
        Data = data;
    }

    public RabbitRequestDto()
    {

    }
}
