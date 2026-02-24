namespace ExtintorCrm.App.Presentation
{
    public class DashboardViewModel : ViewModelBase
    {
        private int _extintoresVencidos;
        private int _extintoresVencendo;
        private int _extintoresVencendo7;
        private int _extintoresVencendo30;
        private int _alvaraVencido;
        private int _alvaraVencendo;
        private int _pagamentosVencidos;
        private int _pagamentosVencendo;
        private int _pagamentosVencendo30;
        private int _alertasOk;
        private int _alertasVencendo;
        private int _alertasVencidos;
        private double _alertasOkPercent;
        private double _alertasVencendoPercent;
        private double _alertasVencidosPercent;

        public int ExtintoresVencidos { get => _extintoresVencidos; set { _extintoresVencidos = value; OnPropertyChanged(); } }
        public int ExtintoresVencendo { get => _extintoresVencendo; set { _extintoresVencendo = value; OnPropertyChanged(); } }
        public int ExtintoresVencendo7 { get => _extintoresVencendo7; set { _extintoresVencendo7 = value; OnPropertyChanged(); } }
        public int ExtintoresVencendo30 { get => _extintoresVencendo30; set { _extintoresVencendo30 = value; OnPropertyChanged(); } }
        public int AlvaraVencido { get => _alvaraVencido; set { _alvaraVencido = value; OnPropertyChanged(); } }
        public int AlvaraVencendo { get => _alvaraVencendo; set { _alvaraVencendo = value; OnPropertyChanged(); } }
        public int PagamentosVencidos { get => _pagamentosVencidos; set { _pagamentosVencidos = value; OnPropertyChanged(); } }
        public int PagamentosVencendo { get => _pagamentosVencendo; set { _pagamentosVencendo = value; OnPropertyChanged(); } }
        public int PagamentosVencendo30 { get => _pagamentosVencendo30; set { _pagamentosVencendo30 = value; OnPropertyChanged(); } }

        public int AlertasOk { get => _alertasOk; set { _alertasOk = value; OnPropertyChanged(); } }
        public int AlertasVencendo { get => _alertasVencendo; set { _alertasVencendo = value; OnPropertyChanged(); } }
        public int AlertasVencidos { get => _alertasVencidos; set { _alertasVencidos = value; OnPropertyChanged(); } }
        public double AlertasOkPercent { get => _alertasOkPercent; set { _alertasOkPercent = value; OnPropertyChanged(); } }
        public double AlertasVencendoPercent { get => _alertasVencendoPercent; set { _alertasVencendoPercent = value; OnPropertyChanged(); } }
        public double AlertasVencidosPercent { get => _alertasVencidosPercent; set { _alertasVencidosPercent = value; OnPropertyChanged(); } }
    }
}
