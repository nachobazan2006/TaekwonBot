namespace BlazorTaekwondo.Models
{
    public class ExamenDto
    {
        public int Id { get; set; }

        public string NombreApellido { get; set; } = string.Empty;

        public string ExamenRendir { get; set; } = string.Empty;

        public string Madera { get; set; } = string.Empty;

        public string Cinturon { get; set; } = string.Empty;

        public string Graduacion { get; set; } = string.Empty;

        public decimal Monto { get; set; }

        public string Clase { get; set; } = string.Empty;

        public DateTime? FechaExamen { get; set; }
    }
}
