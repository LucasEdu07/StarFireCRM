# Star Fire CRM - Indice de Documentacao

Este indice organiza a documentacao oficial do projeto para evitar duplicidade e manter uma fonte unica por assunto.

## Documentos oficiais

- Operacao do produto (usuario final): `docs/OPERACAO_CLIENTE.md`
- Arquitetura e decisoes tecnicas: `docs/ARQUITETURA.md`
- Playbook de release e instalador: `docs/RELEASE_PLAYBOOK.md`
- Testes smoke (validacao rapida): `docs/TESTES_SMOKE.md`
- QA de fluxos criticos: `docs/QA_FLUXOS_CRITICOS.md`

## Regra de manutencao

- Cada assunto critico deve ter apenas um documento oficial.
- Arquivos legados devem conter apenas redirecionamento para o documento oficial.
- Exemplos de versao devem usar placeholder (`<versao>`), evitando versoes fixas antigas.

## Fluxo recomendado por perfil

- Operacao/suporte: comecar em `docs/OPERACAO_CLIENTE.md`.
- Desenvolvimento: comecar em `README.md` e `docs/ARQUITETURA.md`.
- Publicacao de versao: seguir `docs/RELEASE_PLAYBOOK.md`.
