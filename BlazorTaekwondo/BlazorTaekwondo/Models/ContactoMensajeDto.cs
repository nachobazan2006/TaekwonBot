namespace BlazorTaekwondo.Models
{
    public class ContactoMensajeDto
    {
        public int Id { get; set; }
        public string Numero { get; set; } = string.Empty;
        public DateTime FechaContacto { get; set; } = DateTime.Today;
        public string Estado { get; set; } = string.Empty;
        public bool MensajeEnviado { get; set; }
        public DateTime? FechaUltimoMensaje { get; set; }
    }
}
