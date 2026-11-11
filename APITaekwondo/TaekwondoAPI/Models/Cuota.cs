using System.ComponentModel.DataAnnotations;

namespace TaekwondoAPI.Models
{
    public class Cuota
    {
        public int Id { get; set; }
        public int? AlumnoClubId { get; set; }
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        public string Estado { get; set; } = "active";
        public int Version { get; set; } = 1;

        [Required]
        public string NombreApellido { get; set; } = "";

        [Required]
        public string Mes { get; set; } = "";

        public int Monto { get; set; }

        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        public string Clase { get; set; } = "";

        [Required]
        public string MedioPago { get; set; } = "Efectivo";

        public string EmailRecibo { get; set; } = "";

        public bool ReciboEnviado { get; set; }

        public DateTime? FechaEnvioRecibo { get; set; }

        public string ErrorEnvioRecibo { get; set; } = "";
    }
}
