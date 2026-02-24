namespace ExtintorCrm.App.Domain
{
    public class ConfiguracaoAlerta
    {
        public int Id { get; set; } = 1;
        public bool Alerta7Dias { get; set; } = true;
        public bool Alerta15Dias { get; set; } = true;
        public bool Alerta30Dias { get; set; } = true;
    }
}
