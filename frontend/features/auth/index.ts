// Barrel for the auth feature module.
export { AuthHeading } from './AuthHeading';
export { IconField, type IconFieldProps } from './IconField';
export { useApiFormError, type UseApiFormError } from './use-api-form-error';
export {
  registerSchema,
  loginSchema,
  otpSchema,
  normaliseMobile,
  normaliseIdentifier,
  isEmailIdentifier,
  type RegisterFormValues,
  type LoginFormValues,
  type OtpFormValues,
} from './schemas';
export {
  setOtpHandoff,
  getOtpHandoff,
  clearOtpHandoff,
  type OtpHandoff,
} from './otp-handoff';
