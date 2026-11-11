namespace TaekwondoAPI.Models
{
    public class AlumnoClub
    {
        public int Id { get; set; }
        public string NombreApellido { get; set; } = string.Empty;
        public string Graduacion { get; set; } = string.Empty;
        public string Horario { get; set; } = string.Empty;
        public string FichaMedica { get; set; } = string.Empty;
        public string Socio { get; set; } = string.Empty;
        public string Gmail { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
    }
}
