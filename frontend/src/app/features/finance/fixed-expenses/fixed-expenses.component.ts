import { Component } from '@angular/core';
import { FixedMovementsComponent } from '../fixed/fixed-movements.component';

@Component({
  selector: 'app-fixed-expenses',
  standalone: true,
  imports: [FixedMovementsComponent],
  template: `<app-fixed-movements movementType="EXPENSE" />`,
})
export class FixedExpensesComponent {}
