namespace TaekwondoAPI.Services
{
    public interface IReciboPdfService
    {
        byte[] CrearRecibo(ReciboPdfData data);
    }
}
