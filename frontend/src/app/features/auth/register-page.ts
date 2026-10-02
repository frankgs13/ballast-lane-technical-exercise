import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors, toApiError } from '../../core/http/api-error';

/** Mirrors the API rules (PasswordPolicy / User) for instant feedback; the API remains the source of truth. */
export const PASSWORD_RULES: readonly { label: string; test: (value: string) => boolean }[] = [
  { label: '8 to 128 characters', test: (v) => v.length >= 8 && v.length <= 128 },
  { label: 'An uppercase letter', test: (v) => /\p{Lu}/u.test(v) },
  { label: 'A lowercase letter', test: (v) => /\p{Ll}/u.test(v) },
  { label: 'A digit', test: (v) => /\d/.test(v) },
];

export const passwordPolicyValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = String(control.value ?? '');
  return !value || PASSWORD_RULES.every((rule) => rule.test(value)) ? null : { passwordPolicy: true };
};

export const passwordsMatchValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
};

@Component({
  selector: 'app-register-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './register-page.html',
  styleUrl: './auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly hidePassword = signal(true);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50), Validators.pattern(/^[a-zA-Z0-9._-]+$/)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
      password: ['', [Validators.required, passwordPolicyValidator]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatchValidator },
  );

  private readonly password = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });
  protected readonly passwordChecks = computed(() =>
    PASSWORD_RULES.map((rule) => ({ label: rule.label, ok: rule.test(this.password()) })),
  );

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { username, email, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);
    this.auth
      .register({ username, email, password })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => void this.router.navigateByUrl('/tasks'),
        error: (error: unknown) => {
          const apiError = toApiError(error);
          const unmatched = applyServerErrors(this.form, apiError.fieldErrors);
          const hasFieldErrors = Object.keys(apiError.fieldErrors).length > 0;
          this.error.set(unmatched.length ? unmatched.join(' ') : hasFieldErrors ? null : apiError.message);
        },
      });
  }
}
