import { Component } from '@angular/core';
import { FinancialMovementsComponent } from '../movements/financial-movements.component';

@Component({
  selector: 'app-incomes',
  standalone: true,
  imports: [FinancialMovementsComponent],
  template: `<app-financial-movements movementType="INCOME" [showBack]="true" />`,
})
export class IncomesComponent {}
