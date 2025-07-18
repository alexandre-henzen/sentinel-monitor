import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class LoadingService {
  private _loading = new BehaviorSubject<boolean>(false);
  private _loadingCount = 0;

  public loading$ = this._loading.asObservable();

  constructor() {}

  setLoading(loading: boolean): void {
    if (loading) {
      this._loadingCount++;
    } else {
      this._loadingCount--;
    }

    if (this._loadingCount < 0) {
      this._loadingCount = 0;
    }

    this._loading.next(this._loadingCount > 0);
  }

  show(): void {
    this.setLoading(true);
  }

  hide(): void {
    this.setLoading(false);
  }

  get isLoading(): boolean {
    return this._loading.value;
  }
}