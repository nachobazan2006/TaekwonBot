namespace TaekwondoAPI.Services
{
    public class ReciboPdfData
    {
        public string Alumno { get; set; } = string.Empty;
        public string Mes { get; set; } = string.Empty;
        public string Monto { get; set; } = string.Empty;
        public string MedioPago { get; set; } = string.Empty;
        public string Clase { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
    }
}
