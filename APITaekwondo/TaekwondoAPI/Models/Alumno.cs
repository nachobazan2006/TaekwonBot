namespace TaekwondoAPI.Models
{
    public class Alumno
    {
        public int Id { get; set; }               
        public string Numero { get; set; } = "";
        public DateTime FechaContacto { get; set; }
        public string Estado { get; set; } = "";
        public bool MensajeEnviado { get; set; }
        public DateTime? FechaUltimoMensaje { get; set; }
    }

}


