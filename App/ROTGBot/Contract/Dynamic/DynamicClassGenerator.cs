using System.Reflection.Emit;
using System.Reflection;

namespace Contract.Dynamic
{
    public static class DynamicClassHolder
    {
        private static Dictionary<string, Type> _dynamicClasses = new Dictionary<string, Type>();
    }

    public static class DynamicClassGenerator
    {
        public static Type CreateType(string className, Dictionary<string, Type> properties)
        {
            // 1. Создаем имя сборки и модуль
            var assemblyName = new AssemblyName($"DynamicAssembly_{Guid.NewGuid()}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");

            // 2. Создаем публичный класс
            var typeBuilder = moduleBuilder.DefineType(
                className,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout
            );

            // 3. Генерируем свойства
            foreach (var prop in properties)
            {
                CreateProperty(typeBuilder, prop.Key, prop.Value);
            }

            // 4. Компилируем тип
            return typeBuilder.CreateType();
        }

        private static void CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
        {
            // Создаем приватное поле: private T _propertyName;
            var fieldBuilder = typeBuilder.DefineField($"_{propertyName.ToLower()}", propertyType, FieldAttributes.Private);

            // Создаем публичное свойство: public T PropertyName { get; set; }
            var propBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

            // Флаги для методов свойства
            var methodAttrs = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

            // Создаем метод GET
            var getMethodBuilder = typeBuilder.DefineMethod($"get_{propertyName}", methodAttrs, propertyType, Type.EmptyTypes);
            var getIL = getMethodBuilder.GetILGenerator();
            getIL.Emit(OpCodes.Ldarg_0);        // Загружаем текущий экземпляр (this)
            getIL.Emit(OpCodes.Ldfld, fieldBuilder); // Загружаем значение поля
            getIL.Emit(OpCodes.Ret);             // Возвращаем значение

            // Создаем метод SET
            var setMethodBuilder = typeBuilder.DefineMethod($"set_{propertyName}", methodAttrs, null, new[] { propertyType });
            var setIL = setMethodBuilder.GetILGenerator();
            setIL.Emit(OpCodes.Ldarg_0);        // Загружаем this
            setIL.Emit(OpCodes.Ldarg_1);        // Загружаем передаваемое значение (value)
            setIL.Emit(OpCodes.Stfld, fieldBuilder); // Записываем в поле
            setIL.Emit(OpCodes.Ret);

            // Привязываем методы к свойству
            propBuilder.SetGetMethod(getMethodBuilder);
            propBuilder.SetSetMethod(setMethodBuilder);
        }
    }
}
