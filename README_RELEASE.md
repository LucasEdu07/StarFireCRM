# Star Fire - Guia Rápido de Release (Offline)

Este guia descreve como gerar uma nova versão para cliente offline sem perder dados.

## 1) Pré-requisitos
- .NET SDK 8 instalado.
- Inno Setup 6 instalado (com `iscc.exe` no `PATH`) para gerar instalador.
- Projeto compilando localmente.

## 2) Estrutura de dados (importante)
- O app instala em `C:\Program Files\Star Fire` (ou pasta escolhida no setup).
- Os dados do cliente ficam em `%LocalAppData%\StarFire\data`.
- Atualizações de versão **não** devem apagar essa pasta.

## 3) Checklist pré-release
1. Atualize a versão desejada (ex.: `1.0.1`).
2. Execute build:
```powershell
dotnet build .\ExtintorCrm.App\ExtintorCrm.App.csproj
```
3. Rode o app e valide:
   - Dashboard, Clientes, Pagamentos, Configurações.
   - Backup/Restore.
   - Tema Light/Dark.
   - Tour de Ajuda.

## 4) Gerar pacote publicado
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-StarFire.ps1 -Version 1.0.1
```

Saída:
- `artifacts\publish\StarFire-1.0.1\`

## 5) Gerar instalador (recomendado para cliente leigo)
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-StarFire.ps1 -Version 1.0.1 -BuildInstaller
```

Saída:
- `artifacts\installer\StarFire-Setup-1.0.1.exe`

## 6) Validação rápida do instalador
1. Instale versão anterior.
2. Cadastre um cliente de teste.
3. Execute o setup da nova versão.
4. Abra o app e confirme:
   - dados continuam disponíveis;
   - versão nova abriu normalmente.

## 7) Entrega para cliente final
Enviar apenas:
- `StarFire-Setup-<versão>.exe`

Instrução para cliente:
1. Fechar o app.
2. Rodar o instalador.
3. Avançar até concluir.
4. Abrir o app normalmente.

## 8) Rollback (se necessário)
1. Instalar versão anterior.
2. Restaurar backup em:
- `%LocalAppData%\StarFire\data\backups`

## 9) Problemas comuns
- Erro de restore/publish com SSL/NuGet: validar acesso de rede/certificados da máquina.
- App aberto durante update: fechar o app antes de instalar.
- Sem Inno Setup: publicar sem instalador e empacotar manualmente (não recomendado para cliente leigo).
