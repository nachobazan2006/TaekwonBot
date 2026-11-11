namespace TaekwondoAPI.Models
{
    public class ConfiguracionMensajes
    {
        public int Id { get; set; }
        public int DiasRecordatorio { get; set; } = 7; // Valor predeterminado de 7 días
        public bool ReenviarAutomaticamente { get; set; } = true;
    }
}
