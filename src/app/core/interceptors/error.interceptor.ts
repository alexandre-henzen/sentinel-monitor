import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { AuthService } from '../services/auth.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const messageService = inject(MessageService);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error) => {
      let errorMessage = 'Erro inesperado';
      
      if (error.status === 401) {
        errorMessage = 'Não autorizado. Redirecionando para login...';
        authService.logout();
      } else if (error.status === 403) {
        errorMessage = 'Acesso negado. Você não tem permissão para esta operação.';
      } else if (error.status === 404) {
        errorMessage = 'Recurso não encontrado.';
      } else if (error.status === 500) {
        errorMessage = 'Erro interno do servidor. Tente novamente mais tarde.';
      } else if (error.status === 0) {
        errorMessage = 'Erro de conexão. Verifique sua conexão com a internet.';
      } else if (error.error && error.error.message) {
        errorMessage = error.error.message;
      }

      messageService.add({
        severity: 'error',
        summary: 'Erro',
        detail: errorMessage,
        life: 5000
      });

      return throwError(() => error);
    })
  );
};