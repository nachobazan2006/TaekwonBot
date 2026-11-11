namespace BlazorTaekwondo.Models
{
    public class CuotaDto
    {
        public int Id { get; set; }
        public int AlumnoClubId { get; set; }
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        public string Estado { get; set; } = "active";
        public int Version { get; set; } = 1;
        public string NombreApellido { get; set; } = string.Empty;
        public string Mes { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Today;
        public decimal Monto { get; set; }
        public string Clase { get; set; } = string.Empty;
        public string MedioPago { get; set; } = "Efectivo";
        public string EmailRecibo { get; set; } = string.Empty;
        public bool ReciboEnviado { get; set; }
        public DateTime? FechaEnvioRecibo { get; set; }
        public string ErrorEnvioRecibo { get; set; } = string.Empty;
    }
}
