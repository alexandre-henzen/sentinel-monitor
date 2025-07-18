# EAM Frontend - Employee Activity Monitor

Frontend Angular 18 com PrimeNG para o Employee Activity Monitor (EAM) v5.0.

## 🚀 Funcionalidades

### Dashboard Principal
- Métricas em tempo real de agentes e atividades
- Gráficos de produtividade e uso de aplicações
- Cards com estatísticas resumidas
- Filtros por período e agentes

### Timeline de Atividades
- Visualização em timeline e tabela
- Filtros avançados (agente, tipo, aplicação, período)
- Visualizador de screenshots
- Busca em tempo real

### Gerenciamento de Agentes
- Lista completa de agentes com status
- Detalhes de cada agente
- Ações em lote (reiniciar, alterar status, excluir)
- Exportação de dados

### Relatórios
- Relatórios de atividades detalhados
- Relatórios de produtividade
- Relatórios personalizados
- Exportação em CSV/PDF

### Configurações
- Configuração de scoring de produtividade
- Categorias de aplicações
- Configurações do sistema
- Configurações de segurança

## 🛠️ Tecnologias

- **Angular 18** LTS
- **PrimeNG 18** para UI components
- **PrimeFlex** para layout
- **RxJS** para programação reativa
- **Angular OAuth2 OIDC** para autenticação
- **Chart.js** com ngx-charts para gráficos
- **TypeScript** com strict mode

## 📦 Instalação

```bash
# Instalar dependências
npm install

# Executar em desenvolvimento
npm start

# Build para produção
npm run build

# Executar testes
npm test

# Executar linter
npm run lint
```

## 🔧 Configuração

### Ambiente de Desenvolvimento
- API URL: `https://localhost:7001/api/v1`
- OIDC: `https://localhost:7001`
- Redirect URI: `http://localhost:4200/auth/callback`

### Ambiente de Produção
- API URL: `https://api.eam.company.com/api/v1`
- OIDC: `https://auth.eam.company.com`
- Redirect URI: `https://eam.company.com/auth/callback`

## 🏗️ Estrutura do Projeto

```
src/
├── app/
│   ├── core/                 # Serviços singleton
│   │   ├── guards/           # Guards de autenticação
│   │   ├── interceptors/     # HTTP interceptors
│   │   └── services/         # Serviços core
│   ├── shared/               # Componentes reutilizáveis
│   │   └── models/           # Interfaces e DTOs
│   ├── features/             # Módulos por funcionalidade
│   │   ├── auth/             # Autenticação
│   │   ├── dashboard/        # Dashboard principal
│   │   ├── timeline/         # Timeline de atividades
│   │   ├── agents/           # Gestão de agentes
│   │   ├── reports/          # Relatórios
│   │   └── settings/         # Configurações
│   ├── app.component.ts      # Componente raiz
│   ├── app.routes.ts         # Rotas principais
│   └── main.ts               # Bootstrap da aplicação
├── environments/             # Configurações de ambiente
├── assets/                   # Recursos estáticos
└── styles.scss              # Estilos globais
```

## 🔐 Autenticação

O sistema suporta dois tipos de autenticação:

1. **OIDC (OAuth2 + OpenID Connect)**
   - Integração com provedores externos
   - Refresh tokens automático
   - Controle de sessão

2. **Demo (Desenvolvimento)**
   - Usuário: `admin`
   - Senha: `admin`

## 📊 Integrações

### Backend APIs
- **Events API**: Gerenciamento de eventos de atividade
- **Agents API**: Gerenciamento de agentes
- **Auth API**: Autenticação e autorização
- **Screenshots API**: Upload e visualização de screenshots

### Formatos de Dados
- **NDJSON**: Para streaming de eventos
- **JSON**: Para APIs REST
- **CSV/PDF**: Para exportação de relatórios

## 🎨 Temas e Estilos

### PrimeNG Theme
- Tema padrão: `saga-blue`
- Customização via CSS Variables
- Suporte a modo escuro (futuro)

### Responsive Design
- Mobile-first approach
- Breakpoints: sm (576px), md (768px), lg (992px), xl (1200px)
- Componentes adaptáveis

## 🔧 Desenvolvimento

### Comandos Úteis
```bash
# Gerar componente
ng generate component features/example

# Gerar serviço
ng generate service core/services/example

# Gerar guard
ng generate guard core/guards/example

# Executar com proxy
ng serve --proxy-config proxy.conf.json
```

### Padrões de Código
- Componentes standalone (sem NgModules)
- Lazy loading para otimização
- Strict TypeScript
- RxJS para gerenciamento de estado
- OnPush change detection

## 📱 PWA (Preparado)

O projeto está preparado para Progressive Web App:
- Service Worker configurado
- Manifest.json
- Cache strategies
- Offline support (futuro)

## 🧪 Testes

```bash
# Testes unitários
npm run test

# Testes com coverage
npm run test:coverage

# Testes e2e
npm run e2e
```

## 📦 Build e Deploy

```bash
# Build de produção
npm run build

# Análise de bundle
npm run analyze

# Preview do build
npm run preview
```

## 🤝 Contribuição

1. Clone o repositório
2. Crie uma branch para sua feature
3. Commit suas mudanças
4. Push para a branch
5. Abra um Pull Request

## 📄 Licença

Este projeto está licenciado sob a MIT License.

## 🆘 Suporte

Para suporte técnico:
- Email: suporte@eam.company.com
- Documentação: https://docs.eam.company.com
- Issues: https://github.com/company/eam-frontend/issues