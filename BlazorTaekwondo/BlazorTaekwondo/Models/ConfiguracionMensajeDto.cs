namespace BlazorTaekwondo.Models
{
    public class ConfiguracionMensajesDto
    {
        public int DiasRecordatorio { get; set; } = 7;

        public bool ReenviarAutomaticamente { get; set; } = true;
    }
}