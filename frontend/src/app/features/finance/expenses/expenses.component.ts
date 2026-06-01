import { Component } from '@angular/core';
import { FinancialMovementsComponent } from '../movements/financial-movements.component';

@Component({
  selector: 'app-expenses',
  standalone: true,
  imports: [FinancialMovementsComponent],
  template: `<app-financial-movements movementType="EXPENSE" />`,
})
export class ExpensesComponent {}
