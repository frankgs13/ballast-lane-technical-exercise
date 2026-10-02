import { FormControl, FormGroup } from '@angular/forms';

import { passwordPolicyValidator, passwordsMatchValidator } from './register-page';

describe('register validators', () => {
  it.each([
    ['Sup3rSecret', true],
    ['Short1A', false],
    ['alllowercase1', false],
    ['ALLUPPERCASE1', false],
    ['NoDigitsHere', false],
  ])('password policy: %s valid=%s', (password, valid) => {
    expect(passwordPolicyValidator(new FormControl(password)) === null).toBe(valid);
  });

  it('leaves empty values to the required validator', () => {
    expect(passwordPolicyValidator(new FormControl(''))).toBeNull();
  });

  it('flags mismatching confirmation', () => {
    const form = new FormGroup({
      password: new FormControl('Sup3rSecret'),
      confirmPassword: new FormControl('Different1'),
    });

    expect(passwordsMatchValidator(form)).toEqual({ passwordMismatch: true });

    form.controls.confirmPassword.setValue('Sup3rSecret');
    expect(passwordsMatchValidator(form)).toBeNull();
  });
});
