namespace BlazorTaekwondo.Models
{
    public class AlumnoDto
    {
        public int Id { get; set; }
        public string NombreApellido { get; set; } = string.Empty;
        public string Graduacion { get; set; } = string.Empty;
        public string Horario { get; set; } = string.Empty;
        public string FichaMedica { get; set; } = "No";
        public string Socio { get; set; } = "No";
        public string Gmail { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; } = DateTime.Today;
    }
}
