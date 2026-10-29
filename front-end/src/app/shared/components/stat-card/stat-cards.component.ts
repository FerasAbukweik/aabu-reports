import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StatCard } from './stat-card.model';
import { statusTone } from '../../../core/utils/status-utils';

/** Generic stat cards. Pass an array of StatCard and it renders them in a wrapping row. */
@Component({
  selector: 'app-stat-cards',
  imports: [NgTemplateOutlet, RouterLink],
  templateUrl: './stat-cards.component.html',
  styleUrl: './stat-cards.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class StatCardsComponent {
  // input
  readonly cards = input.required<StatCard[]>();
  readonly ariaLabel = input.required<string>();

  // protected
  protected readonly statusTone = statusTone;
}
