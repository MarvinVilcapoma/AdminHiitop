import { Component } from '@angular/core';
import { FinancialMovementsComponent } from '../movements/financial-movements.component';

@Component({
  selector: 'app-finance-history',
  standalone: true,
  imports: [FinancialMovementsComponent],
  template: `<app-financial-movements movementType="" [showBack]="true" />`,
})
export class FinanceHistoryComponent {}
