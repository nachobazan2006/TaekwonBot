using System.ComponentModel.DataAnnotations;

namespace TaekwondoAPI.Models
{
    public class Examen
    {
        public int Id { get; set; }

        [Required]
        public string NombreApellido { get; set; } = "";

        public string ExamenRendir { get; set; } = "";

        public string Madera { get; set; } = "";

        public string Cinturon { get; set; } = "";

        public string Graduacion { get; set; } = "";

        public int Monto { get; set; }

        public string Clase { get; set; } = "";

        public DateTime? FechaExamen { get; set; }
    }
}
