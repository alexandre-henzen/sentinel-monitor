# Employee Activity Monitor (EAM) v5.0 - Padrões de Código e Convenções

## 1. Visão Geral

### 1.1 Objetivos dos Padrões
- **Consistência**: Código uniforme entre toda a equipe
- **Legibilidade**: Código fácil de entender e manter
- **Qualidade**: Redução de bugs e melhoria na manutenibilidade
- **Produtividade**: Desenvolvimento mais eficiente
- **Colaboração**: Facilitação do trabalho em equipe

### 1.2 Aplicabilidade
- **Backend**: C# (.NET 8)
- **Frontend**: TypeScript (Angular 18)
- **Banco de Dados**: SQL (PostgreSQL)
- **Configuração**: JSON/YAML
- **Documentação**: Markdown

## 2. Padrões C# (.NET)

### 2.1 Convenções de Nomenclatura

#### 2.1.1 Classes, Interfaces e Métodos
```csharp
// ✅ Correto - PascalCase
public class UserService
{
    public async Task<User> GetUserByIdAsync(Guid userId) { }
}

public interface IUserRepository
{
    Task<User> FindByIdAsync(Guid id);
}

// ❌ Incorreto
public class userService { }
public class User_Service { }
```

#### 2.1.2 Propriedades e Campos
```csharp
// ✅ Correto
public class User
{
    // Propriedades públicas - PascalCase
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    // Campos privados - camelCase com underscore
    private readonly string _connectionString;
    private readonly ILogger<User> _logger;
    
    // Constantes - PascalCase
    public const string DefaultRole = "User";
    
    // Campos estáticos - PascalCase
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
}
```

#### 2.1.3 Variáveis e Parâmetros
```csharp
// ✅ Correto - camelCase
public async Task<User> CreateUserAsync(string firstName, string lastName)
{
    var newUser = new User
    {
        FirstName = firstName,
        LastName = lastName
    };
    
    var validationResult = await ValidateUserAsync(newUser);
    return validationResult.IsValid ? newUser : null;
}
```

### 2.2 Estrutura de Classes

#### 2.2.1 Ordem dos Membros
```csharp
public class UserService : IUserService
{
    #region Constants
    private const string DefaultRole = "User";
    #endregion
    
    #region Fields
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;
    #endregion
    
    #region Properties
    public string ServiceName => "UserService";
    #endregion
    
    #region Constructor
    public UserService(
        IUserRepository userRepository,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    #endregion
    
    #region Public Methods
    public async Task<User> GetUserByIdAsync(Guid userId)
    {
        // Implementation
    }
    #endregion
    
    #region Private Methods
    private async Task<ValidationResult> ValidateUserAsync(User user)
    {
        // Implementation
    }
    #endregion
}
```

#### 2.2.2 Atributos e Documentação
```csharp
/// <summary>
/// Serviço responsável pelo gerenciamento de usuários
/// </summary>
[Service(ServiceLifetime.Scoped)]
public class UserService : IUserService
{
    /// <summary>
    /// Obtém um usuário pelo ID
    /// </summary>
    /// <param name="userId">ID único do usuário</param>
    /// <returns>Usuário encontrado ou null</returns>
    /// <exception cref="ArgumentException">Lançada quando userId é inválido</exception>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<User> GetUserByIdAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
            
        _logger.LogInformation("Getting user with ID: {UserId}", userId);
        
        return await _userRepository.FindByIdAsync(userId);
    }
}
```

### 2.3 Tratamento de Exceções

#### 2.3.1 Padrões de Exception Handling
```csharp
public async Task<OperationResult<User>> CreateUserAsync(CreateUserRequest request)
{
    try
    {
        // Validação
        if (request == null)
            return OperationResult<User>.Failure("Request cannot be null");
            
        var validationResult = await ValidateCreateUserRequestAsync(request);
        if (!validationResult.IsValid)
            return OperationResult<User>.Failure(validationResult.Errors);
        
        // Operação principal
        var user = await _userRepository.CreateAsync(request.ToUser());
        
        _logger.LogInformation("User created successfully: {UserId}", user.Id);
        return OperationResult<User>.Success(user);
    }
    catch (DuplicateUserException ex)
    {
        _logger.LogWarning(ex, "Attempt to create duplicate user: {Email}", request.Email);
        return OperationResult<User>.Failure("User already exists");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error creating user: {Email}", request.Email);
        return OperationResult<User>.Failure("An unexpected error occurred");
    }
}
```

#### 2.3.2 Custom Exceptions
```csharp
namespace EAM.Shared.Exceptions
{
    /// <summary>
    /// Exceção lançada quando um usuário não é encontrado
    /// </summary>
    public class UserNotFoundException : EamException
    {
        public Guid UserId { get; }
        
        public UserNotFoundException(Guid userId) 
            : base($"User with ID {userId} was not found")
        {
            UserId = userId;
        }
        
        public UserNotFoundException(Guid userId, Exception innerException)
            : base($"User with ID {userId} was not found", innerException)
        {
            UserId = userId;
        }
    }
    
    /// <summary>
    /// Exceção base para o sistema EAM
    /// </summary>
    public abstract class EamException : Exception
    {
        protected EamException(string message) : base(message) { }
        protected EamException(string message, Exception innerException) : base(message, innerException) { }
    }
}
```

### 2.4 Async/Await Patterns

#### 2.4.1 Convenções Async
```csharp
// ✅ Correto
public async Task<User> GetUserAsync(Guid userId)
{
    return await _userRepository.FindByIdAsync(userId);
}

public async Task<IEnumerable<User>> GetUsersAsync(int page, int pageSize)
{
    return await _userRepository.GetPagedAsync(page, pageSize);
}

// ✅ Correto - ConfigureAwait(false) em bibliotecas
public async Task<User> GetUserAsync(Guid userId)
{
    return await _userRepository.FindByIdAsync(userId).ConfigureAwait(false);
}

// ❌ Incorreto - Não usar async void (exceto event handlers)
public async void ProcessUser(User user) { }
```

#### 2.4.2 Cancellation Tokens
```csharp
public async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    
    return await _userRepository.FindByIdAsync(userId, cancellationToken);
}

public async Task<IEnumerable<User>> ProcessUsersAsync(
    IEnumerable<Guid> userIds, 
    CancellationToken cancellationToken = default)
{
    var users = new List<User>();
    
    foreach (var userId in userIds)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await GetUserAsync(userId, cancellationToken);
        if (user != null)
            users.Add(user);
    }
    
    return users;
}
```

### 2.5 Dependency Injection

#### 2.5.1 Registro de Serviços
```csharp
// Program.cs
public static void Main(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    
    // Serviços de infraestrutura
    builder.Services.AddDbContext<EamDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
    // Repositórios
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
    
    // Serviços de aplicação
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IActivityService, ActivityService>();
    
    // Serviços de infraestrutura
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
    
    var app = builder.Build();
    app.Run();
}
```

#### 2.5.2 Injeção em Construtores
```csharp
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IMapper _mapper;
    private readonly ILogger<UserController> _logger;
    
    public UserController(
        IUserService userService,
        IMapper mapper,
        ILogger<UserController> logger)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

## 3. Padrões TypeScript/Angular

### 3.1 Convenções de Nomenclatura

#### 3.1.1 Arquivos e Diretórios
```
src/
├── app/
│   ├── core/                    # Módulos core
│   │   ├── services/           # kebab-case
│   │   │   ├── auth.service.ts
│   │   │   └── data.service.ts
│   │   └── guards/
│   │       └── auth.guard.ts
│   ├── shared/                 # Componentes compartilhados
│   │   ├── components/
│   │   │   ├── user-card/      # kebab-case
│   │   │   │   ├── user-card.component.ts
│   │   │   │   ├── user-card.component.html
│   │   │   │   └── user-card.component.scss
│   │   │   └── data-table/
│   │   └── models/
│   │       ├── user.model.ts
│   │       └── activity.model.ts
│   └── features/               # Funcionalidades
│       ├── dashboard/
│       └── reports/
```

#### 3.1.2 Classes e Interfaces
```typescript
// ✅ Correto - PascalCase para classes
export class UserService {
  constructor(private http: HttpClient) {}
}

export class UserCardComponent implements OnInit {
  // Propriedades públicas - camelCase
  public userName: string;
  public userEmail: string;
  
  // Propriedades privadas - camelCase com underscore
  private _isLoading: boolean = false;
  private readonly _apiUrl = '/api/users';
  
  // Constantes - UPPER_CASE
  private static readonly DEFAULT_PAGE_SIZE = 20;
}

// ✅ Correto - Interfaces com prefixo I (opcional)
export interface IUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
}

// ✅ Correto - Tipos sem prefixo
export type UserStatus = 'active' | 'inactive' | 'pending';
export type ActivityType = 'window' | 'web' | 'application';
```

### 3.2 Estrutura de Componentes

#### 3.2.1 Componente Base
```typescript
import { Component, OnInit, OnDestroy, Input, Output, EventEmitter } from '@angular/core';
import { Observable, Subject, takeUntil } from 'rxjs';

@Component({
  selector: 'app-user-card',
  templateUrl: './user-card.component.html',
  styleUrls: ['./user-card.component.scss']
})
export class UserCardComponent implements OnInit, OnDestroy {
  // Inputs
  @Input() user: IUser | null = null;
  @Input() isLoading: boolean = false;
  
  // Outputs
  @Output() userClick = new EventEmitter<IUser>();
  @Output() userEdit = new EventEmitter<IUser>();
  
  // Public properties
  public displayName: string = '';
  
  // Private properties
  private destroy$ = new Subject<void>();
  
  constructor(
    private userService: UserService,
    private logger: LoggerService
  ) {}
  
  ngOnInit(): void {
    this.initializeComponent();
    this.subscribeToUserChanges();
  }
  
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
  
  // Public methods
  public onUserClick(): void {
    if (this.user) {
      this.userClick.emit(this.user);
    }
  }
  
  public onEditClick(): void {
    if (this.user) {
      this.userEdit.emit(this.user);
    }
  }
  
  // Private methods
  private initializeComponent(): void {
    if (this.user) {
      this.displayName = `${this.user.firstName} ${this.user.lastName}`;
    }
  }
  
  private subscribeToUserChanges(): void {
    this.userService.getUserUpdates()
      .pipe(takeUntil(this.destroy$))
      .subscribe(user => {
        this.handleUserUpdate(user);
      });
  }
  
  private handleUserUpdate(user: IUser): void {
    if (this.user && this.user.id === user.id) {
      this.user = user;
      this.initializeComponent();
    }
  }
}
```

#### 3.2.2 Template Guidelines
```html
<!-- user-card.component.html -->
<div class="user-card" 
     [class.loading]="isLoading"
     (click)="onUserClick()">
  
  <!-- Loading state -->
  <div *ngIf="isLoading" class="loading-indicator">
    <mat-spinner diameter="20"></mat-spinner>
  </div>
  
  <!-- User content -->
  <div *ngIf="!isLoading && user" class="user-content">
    <div class="user-avatar">
      <img [src]="user.avatarUrl || '/assets/default-avatar.png'" 
           [alt]="displayName">
    </div>
    
    <div class="user-info">
      <h3 class="user-name">{{ displayName }}</h3>
      <p class="user-email">{{ user.email }}</p>
      <span class="user-status" 
            [class]="'status-' + user.status">
        {{ user.status | titlecase }}
      </span>
    </div>
    
    <div class="user-actions">
      <button mat-icon-button
              (click)="onEditClick(); $event.stopPropagation()"
              matTooltip="Editar usuário">
        <mat-icon>edit</mat-icon>
      </button>
    </div>
  </div>
  
  <!-- Empty state -->
  <div *ngIf="!isLoading && !user" class="empty-state">
    <p>Nenhum usuário selecionado</p>
  </div>
</div>
```

### 3.3 Serviços e HTTP

#### 3.3.1 Service Pattern
```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject, throwError } from 'rxjs';
import { map, catchError, tap } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly apiUrl = '/api/users';
  private usersSubject = new BehaviorSubject<IUser[]>([]);
  
  public users$ = this.usersSubject.asObservable();
  
  constructor(
    private http: HttpClient,
    private logger: LoggerService
  ) {}
  
  public getUsers(page: number = 1, pageSize: number = 20): Observable<PagedResponse<IUser>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    
    return this.http.get<PagedResponse<IUser>>(this.apiUrl, { params })
      .pipe(
        tap(response => this.logger.debug('Users loaded', response)),
        catchError(error => this.handleError('getUsers', error))
      );
  }
  
  public getUserById(id: string): Observable<IUser> {
    return this.http.get<IUser>(`${this.apiUrl}/${id}`)
      .pipe(
        catchError(error => this.handleError('getUserById', error))
      );
  }
  
  public createUser(user: CreateUserRequest): Observable<IUser> {
    return this.http.post<IUser>(this.apiUrl, user)
      .pipe(
        tap(createdUser => {
          this.logger.info('User created', createdUser);
          this.refreshUsers();
        }),
        catchError(error => this.handleError('createUser', error))
      );
  }
  
  public updateUser(id: string, user: UpdateUserRequest): Observable<IUser> {
    return this.http.put<IUser>(`${this.apiUrl}/${id}`, user)
      .pipe(
        tap(updatedUser => {
          this.logger.info('User updated', updatedUser);
          this.refreshUsers();
        }),
        catchError(error => this.handleError('updateUser', error))
      );
  }
  
  private refreshUsers(): void {
    this.getUsers().subscribe(response => {
      this.usersSubject.next(response.data);
    });
  }
  
  private handleError(operation: string, error: any): Observable<never> {
    this.logger.error(`${operation} failed`, error);
    return throwError(() => new Error(`${operation} failed: ${error.message}`));
  }
}
```

### 3.4 Estado com NgRx (quando aplicável)

#### 3.4.1 Actions
```typescript
// user.actions.ts
import { createAction, props } from '@ngrx/store';

// Load Users
export const loadUsers = createAction(
  '[User] Load Users',
  props<{ page: number; pageSize: number }>()
);

export const loadUsersSuccess = createAction(
  '[User] Load Users Success',
  props<{ users: IUser[]; totalCount: number }>()
);

export const loadUsersFailure = createAction(
  '[User] Load Users Failure',
  props<{ error: string }>()
);

// Create User
export const createUser = createAction(
  '[User] Create User',
  props<{ user: CreateUserRequest }>()
);

export const createUserSuccess = createAction(
  '[User] Create User Success',
  props<{ user: IUser }>()
);

export const createUserFailure = createAction(
  '[User] Create User Failure',
  props<{ error: string }>()
);
```

#### 3.4.2 Reducer
```typescript
// user.reducer.ts
import { createReducer, on } from '@ngrx/store';
import * as UserActions from './user.actions';

export interface UserState {
  users: IUser[];
  totalCount: number;
  isLoading: boolean;
  error: string | null;
  selectedUser: IUser | null;
}

export const initialState: UserState = {
  users: [],
  totalCount: 0,
  isLoading: false,
  error: null,
  selectedUser: null
};

export const userReducer = createReducer(
  initialState,
  
  // Load Users
  on(UserActions.loadUsers, (state) => ({
    ...state,
    isLoading: true,
    error: null
  })),
  
  on(UserActions.loadUsersSuccess, (state, { users, totalCount }) => ({
    ...state,
    users,
    totalCount,
    isLoading: false,
    error: null
  })),
  
  on(UserActions.loadUsersFailure, (state, { error }) => ({
    ...state,
    isLoading: false,
    error
  })),
  
  // Create User
  on(UserActions.createUser, (state) => ({
    ...state,
    isLoading: true,
    error: null
  })),
  
  on(UserActions.createUserSuccess, (state, { user }) => ({
    ...state,
    users: [...state.users, user],
    isLoading: false,
    error: null
  })),
  
  on(UserActions.createUserFailure, (state, { error }) => ({
    ...state,
    isLoading: false,
    error
  }))
);
```

## 4. Padrões SQL/Database

### 4.1 Convenções de Nomenclatura

#### 4.1.1 Tabelas e Colunas
```sql
-- ✅ Correto - snake_case
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(100) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Foreign keys
CREATE TABLE user_activities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id),
    activity_type VARCHAR(50) NOT NULL,
    application_name VARCHAR(255),
    window_title VARCHAR(500),
    url TEXT,
    duration_seconds INTEGER NOT NULL,
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
```

#### 4.1.2 Índices e Constraints
```sql
-- Índices
CREATE INDEX idx_users_username ON users(username);
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_is_active ON users(is_active);

CREATE INDEX idx_user_activities_user_id ON user_activities(user_id);
CREATE INDEX idx_user_activities_timestamp ON user_activities(timestamp);
CREATE INDEX idx_user_activities_type ON user_activities(activity_type);

-- Constraints
ALTER TABLE users ADD CONSTRAINT chk_users_email_format 
    CHECK (email ~* '^[A-Za-z0-9._%-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$');

ALTER TABLE user_activities ADD CONSTRAINT chk_activities_duration_positive 
    CHECK (duration_seconds > 0);
```

### 4.2 Stored Procedures e Functions

#### 4.2.1 Naming Convention
```sql
-- Functions - snake_case com prefixo
CREATE OR REPLACE FUNCTION fn_get_user_productivity_score(
    p_user_id UUID,
    p_from_date DATE,
    p_to_date DATE
) RETURNS DECIMAL(5,2)
LANGUAGE plpgsql
AS $$
DECLARE
    v_productive_time INTEGER;
    v_total_time INTEGER;
    v_score DECIMAL(5,2);
BEGIN
    -- Implementation
    SELECT 
        SUM(CASE WHEN activity_type = 'productive' THEN duration_seconds ELSE 0 END),
        SUM(duration_seconds)
    INTO v_productive_time, v_total_time
    FROM user_activities
    WHERE user_id = p_user_id
      AND timestamp::DATE BETWEEN p_from_date AND p_to_date;
    
    IF v_total_time = 0 THEN
        RETURN 0.00;
    END IF;
    
    v_score := (v_productive_time::DECIMAL / v_total_time::DECIMAL) * 100;
    
    RETURN ROUND(v_score, 2);
END;
$$;
```

## 5. Padrões de Documentação

### 5.1 Comentários de Código

#### 5.1.1 XML Documentation (C#)
```csharp
/// <summary>
/// Representa um usuário do sistema EAM
/// </summary>
/// <remarks>
/// Esta classe contém informações básicas do usuário e métodos
/// para validação e manipulação de dados
/// </remarks>
public class User
{
    /// <summary>
    /// Obtém ou define o identificador único do usuário
    /// </summary>
    /// <value>GUID que identifica unicamente o usuário</value>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Valida se o email do usuário está em formato válido
    /// </summary>
    /// <returns>True se o email é válido, False caso contrário</returns>
    /// <exception cref="ArgumentNullException">
    /// Lançada quando o email é null ou vazio
    /// </exception>
    public bool IsEmailValid()
    {
        // Implementation
    }
}
```

#### 5.1.2 JSDoc (TypeScript)
```typescript
/**
 * Serviço responsável pela autenticação de usuários
 * 
 * @example
 * ```typescript
 * const authService = new AuthService();
 * const result = await authService.login('user@example.com', 'password');
 * ```
 */
export class AuthService {
  
  /**
   * Autentica um usuário com email e senha
   * 
   * @param email - Email do usuário
   * @param password - Senha do usuário
   * @returns Promise que resolve com o resultado da autenticação
   * 
   * @throws {AuthenticationError} Quando as credenciais são inválidas
   * @throws {NetworkError} Quando há problemas de conectividade
   * 
   * @example
   * ```typescript
   * const result = await authService.login('user@example.com', 'password');
   * if (result.success) {
   *   console.log('Login successful');
   * }
   * ```
   */
  public async login(email: string, password: string): Promise<AuthResult> {
    // Implementation
  }
}
```

### 5.2 README e Documentação

#### 5.2.1 Template README
```markdown
# EAM Component Name

Brief description of the component.

## 📋 Table of Contents
- [Installation](#installation)
- [Usage](#usage)
- [API Reference](#api-reference)
- [Configuration](#configuration)
- [Contributing](#contributing)

## 🚀 Installation

```bash
npm install @eam/component-name
```

## 📖 Usage

```typescript
import { ComponentName } from '@eam/component-name';

const component = new ComponentName();
component.doSomething();
```

## 📚 API Reference

### Methods

#### `doSomething(param: string): Promise<Result>`

Description of the method.

**Parameters:**
- `param` (string): Description of parameter

**Returns:**
- `Promise<Result>`: Description of return value

**Example:**
```typescript
const result = await component.doSomething('example');
```

## ⚙️ Configuration

Configuration options available:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `option1` | `string` | `'default'` | Description |
| `option2` | `boolean` | `true` | Description |

## 🤝 Contributing

Please read our [Contributing Guide](CONTRIBUTING.md) for details.
```

## 6. Padrões de Testes

### 6.1 Testes Unitários (C#)

#### 6.1.1 Naming Convention
```csharp
namespace EAM.Tests.Services
{
    [TestClass]
    public class UserServiceTests
    {
        [TestMethod]
        public async Task GetUserByIdAsync_WithValidId_ReturnsUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var expectedUser = new User { Id = userId, Username = "testuser" };
            var mockRepository = new Mock<IUserRepository>();
            mockRepository.Setup(r => r.FindByIdAsync(userId))
                         .ReturnsAsync(expectedUser);
            
            var service = new UserService(mockRepository.Object);
            
            // Act
            var result = await service.GetUserByIdAsync(userId);
            
            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(userId, result.Id);
            Assert.AreEqual("testuser", result.Username);
        }
        
        [TestMethod]
        public async Task GetUserByIdAsync_WithInvalidId_ThrowsArgumentException()
        {
            // Arrange
            var service = new UserService(Mock.Of<IUserRepository>());
            
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => service.GetUserByIdAsync(Guid.Empty));
        }
        
        [TestMethod]
        public async Task GetUserByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockRepository = new Mock<IUserRepository>();
            mockRepository.Setup(r => r.FindByIdAsync(userId))
                         .ReturnsAsync((User)null);
            
            var service = new UserService(mockRepository.Object);
            
            // Act
            var result = await service.GetUserByIdAsync(userId);
            
            // Assert
            Assert.IsNull(result);
        }
    }
}
```

### 6.2 Testes Unitários (TypeScript)

#### 6.2.1 Jasmine/Karma Tests
```typescript
describe('UserService', () => {
  let service: UserService;
  let httpMock: HttpTestingController;
  let loggerSpy: jasmine.SpyObj<LoggerService>;
  
  beforeEach(() => {
    const spy = jasmine.createSpyObj('LoggerService', ['info', 'error', 'debug']);
    
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        UserService,
        { provide: LoggerService, useValue: spy }
      ]
    });
    
    service = TestBed.inject(UserService);
    httpMock = TestBed.inject(HttpTestingController);
    loggerSpy = TestBed.inject(LoggerService) as jasmine.SpyObj<LoggerService>;
  });
  
  afterEach(() => {
    httpMock.verify();
  });
  
  describe('getUsers', () => {
    it('should return users when API call succeeds', () => {
      // Arrange
      const mockUsers: IUser[] = [
        { id: '1', firstName: 'John', lastName: 'Doe', email: 'john@example.com' },
        { id: '2', firstName: 'Jane', lastName: 'Smith', email: 'jane@example.com' }
      ];
      const mockResponse: PagedResponse<IUser> = {
        data: mockUsers,
        totalCount: 2,
        page: 1,
        pageSize: 20,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false
      };
      
      // Act
      service.getUsers(1, 20).subscribe(response => {
        // Assert
        expect(response).toEqual(mockResponse);
        expect(response.data.length).toBe(2);
        expect(response.totalCount).toBe(2);
      });
      
      const req = httpMock.expectOne('/api/users?page=1&pageSize=20');
      expect(req.request.method).toBe('GET');
      req.flush(mockResponse);
    });
    
    it('should handle errors when API call fails', () => {
      // Arrange
      const errorMessage = 'Network error';
      
      // Act & Assert
      service.getUsers().subscribe({
        next: () => fail('Expected error'),
        error: (error) => {
          expect(error.message).toContain('getUsers failed');
          expect(loggerSpy.error).toHaveBeenCalled();
        }
      });
      
      const req = httpMock.expectOne('/api/users?page=1&pageSize=20');
      req.flush(errorMessage, { status: 500, statusText: 'Internal Server Error' });
    });
  });
});
```

## 7. Padrões Git e Versionamento

### 7.1 Commit Messages

#### 7.1.1 Conventional Commits
```bash
# Formato: <type>(<scope>): <description>

# Tipos válidos:
feat: nova funcionalidade
fix: correção de bug
docs: documentação
style: formatação/estilo
refactor: refatoração
test: testes
chore: tarefas de manutenção
perf: melhoria de performance
ci: integração contínua
build: sistema de build

# Exemplos:
feat(auth): add JWT authentication
fix(user): resolve duplicate email validation
docs(api): update authentication endpoints
style(frontend): format user component
refactor(service): extract user validation logic
test(auth): add unit tests for login service
chore(deps): update Angular to v18
perf(db): optimize user queries
ci(pipeline): add automated tests
build(docker): update base image
```

### 7.2 Branch Strategy

#### 7.2.1 GitFlow Adaptado
```bash
# Branches principais
main            # Produção
develop         # Desenvolvimento
release/v1.0.0  # Preparação para release
hotfix/fix-*    # Correções urgentes

# Branches de feature
feature/auth-system
feature/user-management
feature/dashboard
feature/reports

# Branches de bug fix
bugfix/login-issue
bugfix/data-validation

# Naming convention
feature/EAM-123-user-authentication
bugfix/EAM-456-fix-data-export
hotfix/EAM-789-security-vulnerability
```

### 7.3 Pull Request Guidelines

#### 7.3.1 Template PR
```markdown
# Pull Request Title

## 📋 Description
Brief description of changes made.

## 🔄 Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## 🧪 Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed
- [ ] All tests pass

## 📝 Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Code is commented where necessary
- [ ] Documentation updated
- [ ] No new warnings/errors

## 🔗 Related Issues
Fixes #123
Related to #456

## 📸 Screenshots (if applicable)
Add screenshots here.
```

## 8. Configuração de Ferramentas

### 8.1 EditorConfig
```ini
# .editorconfig
root = true

[*]
indent_style = space
indent_size = 2
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{cs,vb}]
indent_size = 4

[*.{js,ts}]
indent_size = 2

[*.{json,yml,yaml}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false
```

### 8.2 Prettier Configuration
```json
{
  "printWidth": 120,
  "tabWidth": 2,
  "useTabs": false,
  "semi": true,
  "singleQuote": true,
  "quoteProps": "as-needed",
  "trailingComma": "es5",
  "bracketSpacing": true,
  "arrowParens": "avoid",
  "endOfLine": "crlf",
  "overrides": [
    {
      "files": "*.html",
      "options": {
        "printWidth": 140
      }
    }
  ]
}
```

### 8.3 ESLint Configuration
```json
{
  "extends": [
    "@angular-eslint/recommended",
    "@angular-eslint/template/process-inline-templates"
  ],
  "rules": {
    "@angular-eslint/directive-selector": [
      "error",
      {
        "type": "attribute",
        "prefix": "app",
        "style": "camelCase"
      }
    ],
    "@angular-eslint/component-selector": [
      "error",
      {
        "type": "element",
        "prefix": "app",
        "style": "kebab-case"
      }
    ],
    "prefer-const": "error",
    "no-var": "error",
    "no-console": "warn",
    "no-debugger": "error"
  }
}
```

## 9. Métricas de Qualidade

### 9.1 Definições de Qualidade
```
Cobertura de Código: >= 80%
Complexidade Ciclomática: <= 10
Duplicação de Código: <= 5%
Linhas por Método: <= 30
Parâmetros por Método: <= 5
Profundidade de Aninhamento: <= 4
```

### 9.2 SonarQube Rules
```yaml
# sonar-project.properties
sonar.projectKey=eam-v5
sonar.organization=company
sonar.sources=src
sonar.tests=tests
sonar.exclusions=**/*.spec.ts,**/*.test.cs,**/node_modules/**
sonar.coverage.exclusions=**/*.spec.ts,**/*.test.cs

# Quality gates
sonar.qualitygate.wait=true
sonar.coverage.threshold=80
sonar.duplicated_lines_density.threshold=5
sonar.cyclomatic_complexity.threshold=10
```

Estes padrões garantem consistência, qualidade e manutenibilidade do código em todo o projeto EAM v5.0.