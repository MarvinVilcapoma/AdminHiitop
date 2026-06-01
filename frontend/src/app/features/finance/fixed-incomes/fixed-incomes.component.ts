import { Component } from '@angular/core';
import { FixedMovementsComponent } from '../fixed/fixed-movements.component';

@Component({
  selector: 'app-fixed-incomes',
  standalone: true,
  imports: [FixedMovementsComponent],
  template: `<app-fixed-movements movementType="INCOME" />`,
})
export class FixedIncomesComponent {}
